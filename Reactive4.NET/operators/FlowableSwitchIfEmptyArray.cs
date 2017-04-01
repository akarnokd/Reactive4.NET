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
    sealed class FlowableSwitchIfEmptyArray<T> : AbstractFlowableOperator<T, T>
    {
        readonly IPublisher<T>[] fallbacks;

        public FlowableSwitchIfEmptyArray(IFlowable<T> source, IPublisher<T>[] fallbacks) : base(source)
        {
            this.fallbacks = fallbacks;
        }

        public override void Subscribe(IFlowableSubscriber<T> subscriber)
        {
            var parent = new SwitchIfEmptyArraySubscriber(subscriber, fallbacks);
            subscriber.OnSubscribe(parent);
            parent.Subscribe(source);
        }

        sealed class SwitchIfEmptyArraySubscriber : SubscriptionArbiter, IFlowableSubscriber<T>
        {
            readonly IFlowableSubscriber<T> actual;

            readonly IPublisher<T>[] fallbacks;

            int index;

            bool hasValue;

            int wip;

            internal SwitchIfEmptyArraySubscriber(IFlowableSubscriber<T> actual, IPublisher<T>[] fallbacks)
            {
                this.actual = actual;
                this.fallbacks = fallbacks;
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
                        if (ArbiterIsCancelled())
                        {
                            return;
                        }
                        if (source == null)
                        {
                            var fs = fallbacks;
                            var n = fs.Length;

                            var i = index;

                            if (i == n)
                            {
                                actual.OnComplete();
                                return;
                            }
                            else
                            {
                                index = i + 1;
                                fs[i].Subscribe(this);
                            }
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
