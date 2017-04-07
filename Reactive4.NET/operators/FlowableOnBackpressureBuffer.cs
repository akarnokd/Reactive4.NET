using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Reactive.Streams;
using Reactive4.NET.utils;
using System.Threading;

namespace Reactive4.NET.operators
{
    sealed class FlowableOnBackpressureBuffer<T> : AbstractFlowableOperator<T, T>
    {
        readonly int capacityHint;

        readonly BufferStrategy strategy;

        readonly Action<T> onDrop;

        public FlowableOnBackpressureBuffer(IFlowable<T> source, int capacityHint, BufferStrategy strategy, Action<T> onDrop) : base(source)
        {
            this.capacityHint = capacityHint;
            this.strategy = strategy;
            this.onDrop = onDrop;
        }

        public override void Subscribe(IFlowableSubscriber<T> subscriber)
        {
            source.Subscribe(new OnBackpressureBufferSubscriber(subscriber, capacityHint, strategy, onDrop));
        }

        sealed class OnBackpressureBufferSubscriber : IFlowableSubscriber<T>, ISubscription
        {
            readonly IFlowableSubscriber<T> actual;

            readonly int capacityHint;

            readonly BufferStrategy strategy;

            readonly Action<T> onDrop;

            readonly ArrayQueue<T> queue;

            ISubscription upstream;

            int wip;

            bool cancelled;
            bool done;
            Exception error;

            long requested;
            long emitted;

            internal OnBackpressureBufferSubscriber(IFlowableSubscriber<T> actual, int capacityHint, BufferStrategy strategy, Action<T> onDrop)
            {
                this.actual = actual;
                this.capacityHint = capacityHint;
                this.strategy = strategy;
                this.onDrop = onDrop;
                this.queue = new ArrayQueue<T>();
            }

            public void Cancel()
            {
                Volatile.Write(ref cancelled, true);
                upstream.Cancel();
                if (Interlocked.Increment(ref wip) == 1)
                {
                    queue.Clear();
                }
            }

            public void OnComplete()
            {
                if (done)
                {
                    return;
                }
                Volatile.Write(ref done, true);
                Drain();
            }

            public void OnError(Exception cause)
            {
                if (done)
                {
                    return;
                }
                error = cause;
                Volatile.Write(ref done, true);
                Drain();
            }

            public void OnNext(T element)
            {
                if (done)
                {
                    return;
                }
                bool dropped = false;
                T item = default(T);
                bool doError = false;

                lock (this)
                {
                    var q = queue;
                    if (q.Count == capacityHint)
                    {
                        if (strategy == BufferStrategy.ERROR)
                        {
                            doError = true;
                        }
                        else
                        if (strategy == BufferStrategy.DROP_NEWEST)
                        {
                            dropped = q.PollLatestOffered(out item);
                            q.Offer(element);
                        }
                        else
                        if (strategy == BufferStrategy.DROP_OLDEST)
                        {
                            dropped = q.Poll(out item);
                            q.Offer(element);
                        }
                    }
                    else
                    {
                        q.Offer(element);
                    }
                }

                if (doError)
                {
                    upstream.Cancel();
                    OnError(new InvalidOperationException("OnBackpressureBuffer: could not buffer value because the capacity limit has been reached"));
                    return;
                } else
                if (dropped)
                {
                    try
                    {
                        onDrop?.Invoke(item);
                    }
                    catch (Exception ex)
                    {
                        upstream.Cancel();
                        OnError(ex);
                        return;
                    }
                }
                Drain();
            }

            public void OnSubscribe(ISubscription subscription)
            {
                if (SubscriptionHelper.Validate(ref upstream, subscription))
                {
                    actual.OnSubscribe(this);

                    subscription.Request(long.MaxValue);
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
                if (Interlocked.Increment(ref wip) != 1)
                {
                    return;
                }

                int missed = 1;
                var a = actual;
                var q = queue;
                var e = emitted;

                for (;;)
                {
                    long r = Volatile.Read(ref requested);

                    while (e != r)
                    {
                        if (Volatile.Read(ref cancelled))
                        {
                            lock (this)
                            {
                                q.Clear();
                            }
                            return;
                        }

                        bool d = Volatile.Read(ref done);
                        bool empty;
                        T item;

                        lock (this)
                        {
                            empty = !q.Poll(out item);
                        }

                        if (d && empty)
                        {
                            var ex = error;
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

                        a.OnNext(item);

                        e++;
                    }

                    if (e == r)
                    {
                        if (Volatile.Read(ref cancelled))
                        {
                            lock (this)
                            {
                                q.Clear();
                            }
                            return;
                        }

                        bool d = Volatile.Read(ref done);
                        bool empty;

                        lock (this)
                        {
                            empty = q.IsEmpty();
                        }

                        if (d && empty)
                        {
                            var ex = error;
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
        }
    }
}
