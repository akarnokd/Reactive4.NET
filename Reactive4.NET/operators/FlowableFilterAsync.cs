using Reactive.Streams;
using Reactive4.NET.utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Reactive4.NET.operators
{
    sealed class FlowableFilterAsync<T> : AbstractFlowableOperator<T, T>
    {
        readonly Func<T, IPublisher<bool>> predicate;

        readonly int bufferSize;

        public FlowableFilterAsync(IFlowable<T> source, Func<T, IPublisher<bool>> predicate, int bufferSize) : base(source)
        {
            this.predicate = predicate;
            this.bufferSize = bufferSize;
        }

        public override void Subscribe(IFlowableSubscriber<T> subscriber)
        {
            source.Subscribe(new FilterAsyncSubscriber(subscriber, predicate, bufferSize));
        }

        sealed class FilterAsyncSubscriber : IFlowableSubscriber<T>, ISubscription
        {
            readonly IFlowableSubscriber<T> actual;

            readonly Func<T, IPublisher<bool>> predicate;

            readonly int bufferSize;

            readonly int limit;

            readonly Entry[] queue;

            ISubscription upstream;

            long producerIndex;
            long consumerIndex;

            int wip;

            bool done;
            bool cancelled;
            Exception error;

            int state;

            long requested;

            long emitted;

            int consumed;

            static readonly int STATE_FRESH = 0;
            static readonly int STATE_RUNNING = 1;
            static readonly int STATE_RESULT_VALUE = 2;
            static readonly int STATE_RESULT_EMPTY = 3;

            InnerSubscriber inner;

            static readonly InnerSubscriber Cancelled = new InnerSubscriber(null);

            internal FilterAsyncSubscriber(IFlowableSubscriber<T> actual, Func<T, IPublisher<bool>> predicate,
                int bufferSize)
            {
                this.actual = actual;
                this.predicate = predicate;
                this.bufferSize = bufferSize;
                this.limit = bufferSize - (bufferSize >> 2);
                int c = QueueHelper.Pow2(bufferSize);
                this.queue = new Entry[c];
            }

            public void Cancel()
            {
                upstream.Cancel();
                Volatile.Write(ref cancelled, true);
                Interlocked.Exchange(ref inner, Cancelled)?.Cancel();
                if (Interlocked.Increment(ref wip) == 1)
                {
                    var q = queue;
                    ClearQueue(q, q.Length);
                }
            }

            void ClearQueue(Entry[] q, int n)
            {
                for (int i = 0; i < n; i++)
                {
                    q[i].item = default(T);
                }
            }

            public void OnComplete()
            {
                Volatile.Write(ref done, true);
                Drain();
            }

            public void OnError(Exception cause)
            {
                ExceptionHelper.AddException(ref error, cause);
                Volatile.Write(ref done, true);
                Drain();
            }

            public void OnNext(T element)
            {
                var q = queue;
                var m = q.Length - 1;
                var pi = producerIndex;
                var offset = (int)pi & m;
                q[offset].item = element;
                Volatile.Write(ref q[offset].state, 1);
                Volatile.Write(ref producerIndex, pi + 1);
                Drain();
            }

            public void OnSubscribe(ISubscription subscription)
            {
                if (SubscriptionHelper.Validate(ref upstream, subscription))
                {
                    actual.OnSubscribe(this);
                    subscription.Request(bufferSize);
                }
            }

            public void Request(long n)
            {
                if (SubscriptionHelper.Validate(n))
                {
                    SubscriptionHelper.AddRequest(ref requested, n);
                    Drain();
                }
            }

            void InnerComplete(bool item)
            {
                Volatile.Write(ref state, item ? STATE_RESULT_VALUE : STATE_RESULT_EMPTY);
                Drain();
            }

            void InnerError(Exception cause)
            {
                ExceptionHelper.AddException(ref error, cause);
                Volatile.Write(ref state, STATE_RESULT_EMPTY);
                Drain();
            }

            void Drain()
            {
                if (Interlocked.Increment(ref wip) != 1)
                {
                    return;
                }

                int missed = 1;
                var a = actual;
                var q = queue;
                var m = q.Length - 1;
                var e = emitted;
                var c = consumed;
                var ci = consumerIndex;
                var lim = limit;

                for (;;)
                {
                    long r = Volatile.Read(ref requested);

                    while (e != r)
                    {
                        if (Volatile.Read(ref cancelled))
                        {
                            ClearQueue(q, m + 1);
                            return;
                        }

                        bool d = Volatile.Read(ref done);

                        int offset = (int)ci & m;
                        bool empty = Volatile.Read(ref q[offset].state) == 0;

                        if (d && empty)
                        {
                            Exception ex = ExceptionHelper.Terminate(ref error);
                            if (ex != null)
                            {
                                a.OnError(ex);
                            }
                            else
                            {
                                a.OnComplete();
                            }
                            return;
                        }

                        if (empty)
                        {
                            break;
                        }

                        T t = q[offset].item;

                        int s = Volatile.Read(ref state);

                        if (s == STATE_FRESH)
                        {
                            IPublisher<bool> pub;
                            try
                            {
                                pub = predicate(t);
                            }
                            catch (Exception ex)
                            {
                                upstream.Cancel();
                                a.OnError(ex);
                                return;
                            }

                            if (pub is IVariableSource<bool> vu)
                            {
                                q[offset].item = default(T);
                                q[offset].state = 0;

                                if (vu.Value(out bool u))
                                {
                                    if (u)
                                    {
                                        a.OnNext(t);

                                        e++;
                                    }
                                }
                                ci++;

                                if (++c == lim)
                                {
                                    c = 0;
                                    upstream.Request(lim);
                                }
                            }
                            else
                            {
                                InnerSubscriber sub = new InnerSubscriber(this);
                                if (SetInner(sub))
                                {
                                    Volatile.Write(ref state, STATE_RUNNING);
                                    pub.Subscribe(sub);
                                }
                            }
                        }
                        else
                        if (s == STATE_RESULT_EMPTY)
                        {
                            q[offset].item = default(T);
                            q[offset].state = 0;
                            ci++;
                            Volatile.Write(ref state, STATE_FRESH);

                            if (++c == lim)
                            {
                                c = 0;
                                upstream.Request(lim);
                            }
                        }
                        else
                        if (s == STATE_RESULT_VALUE)
                        {
                            q[offset].item = default(T);
                            q[offset].state = 0;

                            a.OnNext(t);

                            e++;
                            ci++;
                            Volatile.Write(ref state, STATE_FRESH);

                            if (++c == lim)
                            {
                                c = 0;
                                upstream.Request(lim);
                            }
                        }
                    }


                    if (e == r)
                    {
                        if (Volatile.Read(ref cancelled))
                        {
                            ClearQueue(q, m + 1);
                            return;
                        }

                        bool d = Volatile.Read(ref done);

                        int offset = (int)ci & m;
                        bool empty = Volatile.Read(ref q[offset].state) == 0;

                        if (d && empty)
                        {
                            Exception ex = ExceptionHelper.Terminate(ref error);
                            if (ex != null)
                            {
                                a.OnError(ex);
                            }
                            else
                            {
                                a.OnComplete();
                            }
                            return;
                        }
                    }

                    int w = Volatile.Read(ref wip);
                    if (w == missed)
                    {
                        consumed = c;
                        emitted = e;
                        consumerIndex = ci;
                        missed = Interlocked.Add(ref wip, -missed);
                        if (missed == 0)
                        {
                            break;
                        }
                    }
                    else
                    {
                        missed = w;
                    }
                }
            }

            bool SetInner(InnerSubscriber b)
            {
                for (;;)
                {
                    var a = Volatile.Read(ref inner);
                    if (a == Cancelled)
                    {
                        return false;
                    }
                    if (Interlocked.CompareExchange(ref inner, b, a) == a)
                    {
                        return true;
                    }
                }
            }

            internal struct Entry
            {
                internal int state;
                internal T item;
            }

            internal class InnerSubscriber : IFlowableSubscriber<bool>
            {
                readonly FilterAsyncSubscriber parent;

                ISubscription upstream;

                bool done;

                internal InnerSubscriber(FilterAsyncSubscriber parent)
                {
                    this.parent = parent;
                }

                public void OnComplete()
                {
                    if (!done)
                    {
                        done = true;
                        parent.InnerComplete(false);
                    }
                }

                public void OnError(Exception cause)
                {
                    if (!done)
                    {
                        done = true;
                        parent.InnerError(cause);
                    }
                }

                public void OnNext(bool element)
                {
                    if (!done)
                    {
                        done = true;
                        upstream.Cancel();
                        parent.InnerComplete(element);
                    }
                }

                public void OnSubscribe(ISubscription subscription)
                {
                    if (SubscriptionHelper.SetOnce(ref upstream, subscription))
                    {
                        subscription.Request(long.MaxValue);
                    }
                }

                internal void Cancel()
                {
                    SubscriptionHelper.Cancel(ref upstream);
                }
            }
        }
    }
}
