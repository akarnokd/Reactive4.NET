using Reactive4.NET.utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Reactive.Streams;
using System.Threading;

namespace Reactive4.NET.operators
{
    sealed class FlowableRepeat<T> : AbstractFlowableOperator<T, T>
    {
        readonly Func<bool> stop;

        readonly long times;

        public FlowableRepeat(IFlowable<T> source, Func<bool> stop, long times) : base(source)
        {
            this.stop = stop;
            this.times = times;
        }
         
        public override void Subscribe(IFlowableSubscriber<T> subscriber)
        {
            var parent = new RepeatSubscriber(subscriber, stop, times, source);
            subscriber.OnSubscribe(parent);
            parent.OnComplete();
        }

        sealed class RepeatSubscriber : SubscriptionArbiter, IFlowableSubscriber<T>
        {
            readonly IFlowableSubscriber<T> actual;

            readonly Func<bool> stop;

            readonly IFlowable<T> source;

            long remaining;

            int wip;

            long produced;

            internal RepeatSubscriber(IFlowableSubscriber<T> actual, Func<bool> stop, long times, IFlowable<T> source)
            {
                this.actual = actual;
                this.stop = stop;
                this.remaining = times;
                this.source = source;
            }

            public void OnComplete()
            {
                if (Interlocked.Increment(ref wip) == 1)
                {
                    do
                    {
                        if (ArbiterIsCancelled())
                        {
                            return;
                        }
                        long r = remaining;
                        if (r == 0)
                        {
                            actual.OnComplete();
                            return;
                        }

                        bool b;

                        try
                        {
                            b = stop();
                        }
                        catch (Exception ex)
                        {
                            actual.OnError(ex);
                            return;
                        }

                        if (b)
                        {
                            actual.OnComplete();
                            return;
                        }
                        long p = produced;
                        if (p != 0)
                        {
                            produced = 0;
                            ArbiterProduced(p);
                        }

                        if (r != long.MaxValue)
                        {
                            remaining = r - 1;
                        }
                        source.Subscribe(this);
                    }
                    while (Interlocked.Decrement(ref wip) != 0);
                }
            }

            public void OnError(Exception cause)
            {
                actual.OnError(cause);
            }

            public void OnNext(T element)
            {
                produced++;
                actual.OnNext(element);
            }

            public void OnSubscribe(ISubscription subscription)
            {
                ArbiterSet(subscription);
            }
        }
    }
}
