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
    sealed class FlowableRetry<T> : AbstractFlowableOperator<T, T>
    {
        readonly Func<Exception, bool> handler;

        readonly long times;

        public FlowableRetry(IFlowable<T> source, Func<Exception, bool> handler, long times) : base(source)
        {
            this.handler = handler;
            this.times = times;
        }
         
        public override void Subscribe(IFlowableSubscriber<T> subscriber)
        {
            var parent = new RetrySubscriber(subscriber, handler, times, source);
            subscriber.OnSubscribe(parent);
            parent.OnError(null);
        }

        sealed class RetrySubscriber : SubscriptionArbiter, IFlowableSubscriber<T>
        {
            readonly IFlowableSubscriber<T> actual;

            readonly Func<Exception, bool> handler;

            readonly IFlowable<T> source;

            long remaining;

            int wip;

            long produced;

            internal RetrySubscriber(IFlowableSubscriber<T> actual, Func<Exception, bool> handler, long times, IFlowable<T> source)
            {
                this.actual = actual;
                this.handler = handler;
                this.remaining = times;
                this.source = source;
            }

            public void OnComplete()
            {
                actual.OnComplete();
            }

            public void OnError(Exception cause)
            {
                if (Interlocked.Increment(ref wip) == 1)
                {
                    do
                    {
                        if (ArbiterIsCancelled())
                        {
                            return;
                        }

                        if (cause != null)
                        {
                            long r = remaining;
                            if (r == 0)
                            {
                                actual.OnError(cause);
                                return;
                            }

                            bool b;

                            try
                            {
                                b = handler(cause);
                            }
                            catch (Exception ex)
                            {
                                actual.OnError(new AggregateException(cause, ex));
                                return;
                            }

                            if (!b)
                            {
                                actual.OnError(cause);
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
                        }
                        source.Subscribe(this);
                    }
                    while (Interlocked.Decrement(ref wip) != 0);
                }
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
