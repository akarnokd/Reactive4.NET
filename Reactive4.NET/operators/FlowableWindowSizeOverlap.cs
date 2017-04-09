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
    sealed class FlowableWindowSizeOverlap<T> : AbstractFlowableOperator<T, IFlowable<T>>
    {
        readonly int size;

        readonly int skip;

        public FlowableWindowSizeOverlap(IFlowable<T> source, int size, int skip) : base(source)
        {
            this.size = size;
            this.skip = skip;
        }

        public override void Subscribe(IFlowableSubscriber<IFlowable<T>> subscriber)
        {
            source.Subscribe(new WindowSizeOverlapSubscriber(subscriber, size, skip));
        }

        sealed class WindowSizeOverlapSubscriber : IFlowableSubscriber<T>, IQueueSubscription<IFlowable<T>>
        {
            readonly IFlowableSubscriber<IFlowable<T>> actual;

            readonly int size;

            readonly int skip;

            readonly ArrayQueue<UnicastProcessor<T>> queue;

            readonly ISimpleQueue<UnicastProcessor<T>> windows;

            ISubscription upstream;

            int firstRequest;

            long requested;

            long emitted;

            int count;

            int index;

            int once;

            int active;

            bool cancelled;

            int wip;

            bool done;
            Exception error;

            bool outputFused;

            static readonly Action<UnicastProcessor<T>> CallComplete = up => up.OnComplete();

            static readonly Action<Exception, UnicastProcessor<T>> CallError = (ex, up) => up.OnError(ex);

            static readonly Action<T, UnicastProcessor<T>> CallNext = (t, up) => up.OnNext(t);

            internal WindowSizeOverlapSubscriber(IFlowableSubscriber<IFlowable<T>> actual, int size, int skip)
            {
                this.actual = actual;
                this.size = size;
                this.skip = skip;
                this.active = 1;
                this.queue = new ArrayQueue<UnicastProcessor<T>>();
                this.windows = new SpscLinkedArrayQueue<UnicastProcessor<T>>(Flowable.BufferSize());
            }

            public void Cancel()
            {
                Volatile.Write(ref cancelled, true);
                if (Interlocked.CompareExchange(ref once, 1, 0) == 0)
                {
                    OnTerminate();
                }
            }

            public void OnTerminate()
            {
                if (Interlocked.Decrement(ref active) == 0)
                {
                    upstream.Cancel();
                    if (Interlocked.Increment(ref wip) == 1)
                    {
                        queue.Clear();
                    }
                }
            }

            public void OnComplete()
            {
                var q = queue;
                q.ForEach(CallComplete);
                Volatile.Write(ref done, true);
                Drain();
            }

            public void OnError(Exception cause)
            {
                var q = queue;
                q.ForEach(cause, CallError);
                q.Clear();
                error = cause;
                Volatile.Write(ref done, true);
                Drain();
            }

            public void OnNext(T element)
            {
                var q = queue;
                int idx = index;

                bool drain = false;

                if (idx == 0)
                {
                    int a = Volatile.Read(ref active);
                    if (a != 0 && Interlocked.CompareExchange(ref active, a + 1, a) == a)
                    {
                        var up = new UnicastProcessor<T>(size, OnTerminate);
                        q.Offer(up);

                        windows.Offer(up);
                        drain = true;
                    }
                }

                q.ForEach(element, CallNext);

                int c = count + 1;

                if (c == size)
                {
                    count = c - skip;

                    q.Poll(out var item);

                    item.OnComplete();
                }
                else
                {
                    count = c;
                }

                if (++idx == skip)
                {
                    index = 0;
                }
                else
                {
                    index = idx;
                }

                if (drain)
                {
                    Drain();
                }
            }

            public void OnSubscribe(ISubscription subscription)
            {
                if (SubscriptionHelper.Validate(ref upstream, subscription))
                {
                    actual.OnSubscribe(this);
                }
            }

            public void Request(long n)
            {
                if (SubscriptionHelper.Validate(n))
                {
                    SubscriptionHelper.AddRequest(ref requested, n);
                    long u = SubscriptionHelper.MultiplyCap(n, size - skip);
                    if (Volatile.Read(ref firstRequest) == 0 && Interlocked.CompareExchange(ref firstRequest, 1, 0) == 0)
                    {
                        u += skip;
                        if (u < 0L)
                        {
                            u = long.MaxValue;
                        }
                    }
                    upstream.Request(u);
                }
            }

            void Drain()
            {
                if (outputFused)
                {
                    SubscriptionHelper.QueueDrainFused(actual, ref wip, windows, ref cancelled, ref done, ref error);
                }
                else
                {
                    SubscriptionHelper.QueueDrain(actual, ref wip, windows, ref requested, ref emitted, ref cancelled, ref done, ref error);
                }
            }

            public int RequestFusion(int mode)
            {
                if ((mode & FusionSupport.ASYNC) != 0)
                {
                    outputFused = true;
                    return FusionSupport.ASYNC;
                }
                return FusionSupport.NONE;
            }

            public bool Offer(IFlowable<T> item)
            {
                throw new InvalidOperationException("Should not be called!");
            }

            public bool Poll(out IFlowable<T> item)
            {

                if (windows.Poll(out var v))
                {
                    item = v;
                    return true;
                }
                item = default(IFlowable<T>);
                return false;
            }

            public bool IsEmpty()
            {
                return windows.IsEmpty();
            }

            public void Clear()
            {
                windows.Clear();
            }
        }
    }
}
