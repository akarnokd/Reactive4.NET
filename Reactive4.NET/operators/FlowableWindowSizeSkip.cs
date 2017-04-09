using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Reactive.Streams;
using System.Threading;

namespace Reactive4.NET.operators
{
    sealed class FlowableWindowSizeSkip<T> : AbstractFlowableOperator<T, IFlowable<T>>
    {
        readonly int size;

        readonly int skip;

        public FlowableWindowSizeSkip(IFlowable<T> source, int size, int skip) : base(source)
        {
            this.size = size;
            this.skip = skip;
        }

        public override void Subscribe(IFlowableSubscriber<IFlowable<T>> subscriber)
        {
            source.Subscribe(new WindowSizeSkipSubscriber(subscriber, size, skip));
        }

        sealed class WindowSizeSkipSubscriber : IFlowableSubscriber<T>, ISubscription
        {
            readonly IFlowableSubscriber<IFlowable<T>> actual;

            readonly int size;

            readonly int skip;

            int active;

            int once;

            int firstRequest;

            int index;

            int count;

            ISubscription upstream;

            UnicastProcessor<T> window;

            internal WindowSizeSkipSubscriber(IFlowableSubscriber<IFlowable<T>> actual, int size, int skip)
            {
                this.actual = actual;
                this.size = size;
                this.skip = skip;
                this.active = 1;
            }

            void OnTerminate()
            {
                if (Interlocked.Decrement(ref active) == 0)
                {
                    upstream.Cancel();
                }
            }

            public void Cancel()
            {
                if (Interlocked.CompareExchange(ref once, 1, 0) == 0)
                {
                    if (Interlocked.Decrement(ref active) == 0)
                    {
                        upstream.Cancel();
                    }
                }
            }

            public void OnComplete()
            {
                var w = window;
                window = null;
                w?.OnComplete();
                if (Volatile.Read(ref once) == 0)
                {
                    actual.OnComplete();
                }
            }

            public void OnError(Exception cause)
            {
                var w = window;
                window = null;
                w?.OnError(cause);
                if (Volatile.Read(ref once) == 0)
                {
                    actual.OnError(cause);
                }
            }

            public void OnNext(T element)
            {
                var w = window;
                int idx = index;
                if (idx == 0)
                {
                    int a = Volatile.Read(ref active);
                    if (a != 0 && Interlocked.CompareExchange(ref active, a + 1, a) == a)
                    {
                        w = new UnicastProcessor<T>(size, OnTerminate);
                        window = w;

                        actual.OnNext(w);
                    }
                }

                if (w != null)
                {
                    w.OnNext(element);
                    int c = count + 1;
                    if (c == size)
                    {
                        count = 0;
                        w.OnComplete();
                        window = null;
                    }
                    else
                    {
                        count = c;
                    }
                }


                if (++idx == skip)
                {
                    index = 0;
                }
                else
                {
                    index = idx;
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
                    long u = SubscriptionHelper.MultiplyCap(n, skip);
                    if (Volatile.Read(ref firstRequest) == 0 && Interlocked.CompareExchange(ref firstRequest, 1, 0) == 0)
                    {
                        if (u != long.MaxValue)
                        {
                            u -= (skip - size);
                        }
                    }
                    upstream.Request(u);
                }
            }
        }
    }
}
