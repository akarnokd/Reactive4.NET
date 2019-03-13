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
    sealed class ParallelFlowableJoin<T> : AbstractFlowableSource<T>
    {
        readonly IParallelFlowable<T> source;

        readonly int prefetch;

        internal ParallelFlowableJoin(IParallelFlowable<T> source, int prefetch)
        {
            this.source = source;
            this.prefetch = prefetch;
        }

        public override void Subscribe(IFlowableSubscriber<T> subscriber)
        {
            var parent = new JoinSubscription(subscriber, source.Parallelism, prefetch);
            subscriber.OnSubscribe(parent);
            source.Subscribe(parent.subscribers);
        }

        sealed class JoinSubscription : ISubscription
        {
            readonly IFlowableSubscriber<T> actual;

            internal readonly InnerSubscriber[] subscribers;

            readonly int bufferSize;

            readonly int limit;

            long requested;

            long emitted;

            int wip;

            bool cancelled;
            int done;
            Exception error;

            internal JoinSubscription(IFlowableSubscriber<T> actual, int n, int bufferSize)
            {
                this.actual = actual;
                this.bufferSize = bufferSize;
                this.done = n;
                this.limit = bufferSize - (bufferSize >> 2);
                var subs = new InnerSubscriber[n];
                for (int i = 0; i < n; i++)
                {
                    subs[i] = new InnerSubscriber(this);
                }
                this.subscribers = subs;
            }

            public void Cancel()
            {
                Volatile.Write(ref cancelled, true);
                var subs = subscribers;
                foreach (var inner in subs)
                {
                    inner.Cancel();
                }
                if (Interlocked.Increment(ref wip) == 1)
                {
                    ClearAll(subs);
                }
            }

            void ClearAll(InnerSubscriber[] subs)
            {
                foreach (var inner in subs)
                {
                    inner.Queue()?.Clear();
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

            void Drain()
            {
                if (Interlocked.Increment(ref wip) == 1)
                {
                    DrainLoop(1);
                }
            }

            void DrainLoop(int missed)
            {
                var a = actual;
                var subs = subscribers;
                var n = subs.Length;
                var e = emitted;
                var lim = limit;

                for (;;)
                {

                    long r = Volatile.Read(ref requested);

                    while (e != r)
                    {
                        if (Volatile.Read(ref cancelled))
                        {
                            ClearAll(subs);
                            return;
                        }

                        bool d = Volatile.Read(ref done) == 0;

                        bool empty = true;
                        bool noRequest = false;
                        foreach (var inner in subs)
                        {
                            var q = inner.Queue();

                            if (q != null)
                            {
                                if (q.Poll(out T t))
                                {
                                    empty = false;
                                    a.OnNext(t);

                                    inner.Produced(limit);
                                    if (++e == r)
                                    {
                                        noRequest = true;
                                        break;
                                    }
                                }
                            }
                        }

                        if (d && empty)
                        {
                            var ex = ExceptionHelper.Terminate(ref error);
                            if (ex == null)
                            {
                                a.OnComplete();
                            }
                            else
                            {
                                a.OnError(ex);
                            }
                            return;
                        }

                        if (empty || noRequest)
                        {
                            break;
                        }
                    }

                    if (e == r)
                    {
                        if (Volatile.Read(ref cancelled))
                        {
                            ClearAll(subs);
                            return;
                        }

                        bool d = Volatile.Read(ref done) == 0;

                        bool empty = true;

                        foreach (var inner in subs)
                        {
                            var q = inner.Queue();
                            if (q != null && !q.IsEmpty())
                            {
                                empty = false;
                                break;
                            }
                        }

                        if (d && empty)
                        {
                            var ex = ExceptionHelper.Terminate(ref error);
                            if (ex == null)
                            {
                                a.OnComplete();
                            }
                            else
                            {
                                a.OnError(ex);
                            }
                            return;
                        }
                    }

                    int w = Volatile.Read(ref wip);
                    if (w == missed)
                    {
                        emitted = e;
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

            void InnerNext(InnerSubscriber inner, T item)
            {
                if (Volatile.Read(ref wip) == 0 && Interlocked.CompareExchange(ref wip, 1, 0) == 0)
                {
                    long r = Volatile.Read(ref requested);
                    long e = emitted;
                    if (e != r)
                    {
                        actual.OnNext(item);
                        emitted = e + 1;
                        inner.Produced(limit);
                    }
                    else
                    {
                        var q = inner.GetOrCreateQueue(limit);
                        q.Offer(item);
                    }
                    int w = Interlocked.Decrement(ref wip);
                    if (w == 0)
                    {
                        return;
                    }
                    DrainLoop(w);
                }
                else
                {
                    var q = inner.GetOrCreateQueue(limit);
                    q.Offer(item);
                    if (Interlocked.Increment(ref wip) != 1)
                    {
                        return;
                    }
                    DrainLoop(1);
                }
            }

            void InnerError(InnerSubscriber inner, Exception cause)
            {
                ExceptionHelper.AddException(ref error, cause);
                Volatile.Write(ref inner.done, true);
                Interlocked.Decrement(ref done);
                Drain();
            }

            void InnerComplete(InnerSubscriber inner)
            {
                Volatile.Write(ref inner.done, true);
                Interlocked.Decrement(ref done);
                Drain();
            }

            internal sealed class InnerSubscriber : IFlowableSubscriber<T>
            {
                readonly JoinSubscription parent;

                ISubscription upstream;

                int produced;

                ISimpleQueue<T> queue;

                internal bool done;

                internal InnerSubscriber(JoinSubscription parent)
                {
                    this.parent = parent;
                }

                public void OnComplete()
                {
                    parent.InnerComplete(this);
                }

                public void OnError(Exception cause)
                {
                    parent.InnerError(this, cause);
                }

                public void OnNext(T element)
                {
                    parent.InnerNext(this, element);
                }

                public void OnSubscribe(ISubscription subscription)
                {
                    if (SubscriptionHelper.SetOnce(ref upstream, subscription))
                    {
                        subscription.Request(parent.bufferSize);
                    }
                }

                internal void Cancel()
                {
                    SubscriptionHelper.Cancel(ref upstream);
                }

                internal void Produced(int limit)
                {
                    int p = produced + 1; 
                    if (p == limit)
                    {
                        produced = 0;
                        upstream.Request(p);
                    }
                    else
                    {
                        produced = p;
                    }
                }

                internal ISimpleQueue<T> Queue()
                {
                    return Volatile.Read(ref queue);
                }

                internal ISimpleQueue<T> GetOrCreateQueue(int bufferSize)
                {
                    var q = Volatile.Read(ref queue);
                    if (q == null)
                    {
                        q = new SpscArrayQueue<T>(bufferSize);
                        Volatile.Write(ref queue, q);
                    }
                    return q;
                }
            }
        }
    }
}
