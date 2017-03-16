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
    sealed class FlowableFlatMapEnumerable<T, R> : AbstractFlowableOperator<T, R>
    {
        readonly Func<T, IEnumerable<R>> mapper;

        readonly int prefetch;

        public FlowableFlatMapEnumerable(IFlowable<T> source, Func<T, IEnumerable<R>> mapper, int prefetch) : base(source)
        {
            this.mapper = mapper;
            this.prefetch = prefetch;
        }

        public override void Subscribe(IFlowableSubscriber<R> subscriber)
        {
            source.Subscribe(new FlatMapEnumerableSubscriber(subscriber, mapper, prefetch));
        }

        sealed class FlatMapEnumerableSubscriber : IFlowableSubscriber<T>, IQueueSubscription<R>
        {
            readonly IFlowableSubscriber<R> actual;

            readonly Func<T, IEnumerable<R>> mapper;

            readonly int prefetch;

            readonly int limit;

            IEnumerator<R> current;

            ISubscription upstream;

            ISimpleQueue<T> queue;

            Exception error;

            long requested;

            long emitted;

            int consumed;

            int wip;

            bool done;
            bool cancelled;

            int fusionMode;

            internal FlatMapEnumerableSubscriber(IFlowableSubscriber<R> actual,
                Func<T, IEnumerable<R>> mapper, int prefetch)
            {
                this.actual = actual;
                this.mapper = mapper;
                this.prefetch = prefetch;
                this.limit = prefetch - (prefetch >> 2);
            }

            void DisposeSilently(IDisposable d)
            {
                try
                {
                    d?.Dispose();
                }
                catch (ObjectDisposedException)
                {

                }
            }

            public void Cancel()
            {
                Volatile.Write(ref cancelled, true);
                upstream.Cancel();
                if (Interlocked.Increment(ref wip) == 1)
                {
                    Clear();
                }
            }

            public void Clear()
            {
                queue.Clear();
                DisposeSilently(current);
                current = null;
            }

            public bool IsEmpty()
            {
                return current == null && queue.IsEmpty();
            }

            public bool Offer(R item)
            {
                throw new InvalidOperationException("Should not be called!");
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
                if (fusionMode == FusionSupport.NONE)
                {
                    queue.Offer(element);
                }
                Drain();
            }

            public void OnSubscribe(ISubscription subscription)
            {
                if (SubscriptionHelper.Validate(ref upstream, subscription))
                {
                    if (subscription is IQueueSubscription<T> qs)
                    {
                        int m = qs.RequestFusion(FusionSupport.ANY);
                        if (m == FusionSupport.SYNC)
                        {
                            fusionMode = m;
                            queue = qs;
                            Volatile.Write(ref done, true);

                            actual.OnSubscribe(this);
                            return;
                        }
                        if (m == FusionSupport.ASYNC)
                        {
                            fusionMode = m;
                            queue = qs;

                            actual.OnSubscribe(this);

                            subscription.Request(prefetch);
                            return;
                        }
                    }

                    queue = new SpscArrayQueue<T>(prefetch);

                    actual.OnSubscribe(this);

                    subscription.Request(prefetch);
                }
            }

            public bool Poll(out R item)
            {
                var en = current;
                if (en == null)
                {
                    for (;;)
                    {
                        if (queue.Poll(out T v))
                        {
                            en = mapper(v).GetEnumerator();
                            if (en.MoveNext())
                            {
                                current = en;
                                break;
                            }
                            DisposeSilently(en);
                        }
                        else
                        {
                            item = default(R);
                            return false;
                        }
                    }
                }

                item = en.Current;
                if (!en.MoveNext())
                {
                    DisposeSilently(en);
                    current = null;
                }
                return true;
            }

            public void Request(long n)
            {
                if (SubscriptionHelper.Validate(n))
                {
                    SubscriptionHelper.AddRequest(ref requested, n);
                    Drain();
                }
            }

            public int RequestFusion(int mode)
            {
                if (fusionMode == FusionSupport.SYNC && (mode & FusionSupport.SYNC) != 0)
                {
                    return FusionSupport.SYNC;
                }
                return FusionSupport.NONE;
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
                var e = emitted;
                var f = consumed;
                var en = current;

                for (;;)
                {
                    if (en == null)
                    {
                        for (;;)
                        {
                            if (Volatile.Read(ref cancelled))
                            {
                                Clear();
                                return;
                            }

                            bool d = Volatile.Read(ref done);

                            bool empty = !q.Poll(out T t);

                            if (d && empty)
                            {
                                var ex = ExceptionHelper.Terminate(ref error);
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

                            bool hasValue;
                            IEnumerator<R> enumerator = null;

                            try
                            {
                                var enumerable = mapper(t);
                                enumerator = enumerable.GetEnumerator();
                                hasValue = enumerator.MoveNext();
                            }
                            catch (Exception ex)
                            {
                                ExceptionHelper.AddException(ref error, ex);
                                hasValue = false;
                                DisposeSilently(enumerator);
                                enumerator = null;
                            }

                            if (hasValue)
                            {
                                en = enumerator;
                                break;
                            }
                        }
                    }

                    if (en != null)
                    {
                        long r = Volatile.Read(ref requested);
                        while (e != r)
                        {
                            if (Volatile.Read(ref cancelled))
                            {
                                Clear();
                                return;
                            }

                            a.OnNext(en.Current);

                            e++;

                            bool b;

                            try
                            {
                                b = en.MoveNext();
                            } catch (Exception ex)
                            {
                                b = false;
                                ExceptionHelper.AddException(ref error, ex);
                                DisposeSilently(en);
                            }
                            if (!b)
                            {
                                en = null;
                                break;
                            }
                        }
                        if (en == null)
                        {
                            continue;
                        }
                    }

                    int w = Volatile.Read(ref wip);
                    if (w == missed) {
                        emitted = e;
                        consumed = f;
                        current = en;
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
        }
    }
}
