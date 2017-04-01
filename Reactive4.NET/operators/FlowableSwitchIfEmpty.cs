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
    sealed class FlowableSwitchIfEmpty<T> : AbstractFlowableOperator<T, T>
    {
        readonly IPublisher<T> fallback;

        public FlowableSwitchIfEmpty(IFlowable<T> source, IPublisher<T> fallback) : base(source)
        {
            this.fallback = fallback;
        }

        public override void Subscribe(IFlowableSubscriber<T> subscriber)
        {
            var parent = new SwitchIfEmptySubscriber(subscriber, fallback);
            subscriber.OnSubscribe(parent);
            parent.Subscribe(source);
        }

        sealed class SwitchIfEmptySubscriber : SubscriptionArbiter, IFlowableSubscriber<T>
        {
            readonly IFlowableSubscriber<T> actual;

            readonly IPublisher<T> fallback;

            bool hasValue;

            int wip;

            internal SwitchIfEmptySubscriber(IFlowableSubscriber<T> actual, IPublisher<T> fallback)
            {
                this.actual = actual;
                this.fallback = fallback;
            }

            public void OnComplete()
            {
                if (hasValue)
                {
                    actual.OnComplete();
                }
                else
                {
                    Subscribe(null);
                }
            }

            public void OnError(Exception cause)
            {
                actual.OnError(cause);
            }

            public void OnNext(T element)
            {
                if (!hasValue)
                {
                    hasValue = true;
                }
                actual.OnNext(element);
            }

            public void OnSubscribe(ISubscription subscription)
            {
                ArbiterSet(subscription);
            }

            internal void Subscribe(IFlowable<T> source)
            {
                if (Interlocked.Increment(ref wip) == 1)
                {
                    do
                    {
                        if (source == null)
                        {
                            hasValue = true;
                            fallback.Subscribe(this);
                        }
                        else
                        {
                            source.Subscribe(this);
                            source = null;
                        }
                    }
                    while (Interlocked.Decrement(ref wip) != 0);
                }
            }
        }
    }
}
