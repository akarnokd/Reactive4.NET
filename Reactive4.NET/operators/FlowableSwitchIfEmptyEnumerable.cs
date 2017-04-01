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
    sealed class FlowableSwitchIfEmptyEnumerable<T> : AbstractFlowableOperator<T, T>
    {
        readonly IEnumerable<IPublisher<T>> fallbacks;

        public FlowableSwitchIfEmptyEnumerable(IFlowable<T> source, IEnumerable<IPublisher<T>> fallbacks) : base(source)
        {
            this.fallbacks = fallbacks;
        }

        public override void Subscribe(IFlowableSubscriber<T> subscriber)
        {
            IEnumerator<IPublisher<T>> en;

            try
            {
                en = fallbacks.GetEnumerator();
            }
            catch (Exception ex)
            {
                subscriber.OnSubscribe(EmptySubscription<T>.Instance);
                subscriber.OnError(ex);
                return;
            }

            var parent = new SwitchIfEmptyEnumerableSubscriber(subscriber, en);
            subscriber.OnSubscribe(parent);
            parent.Subscribe(source);
        }

        sealed class SwitchIfEmptyEnumerableSubscriber : SubscriptionArbiter, IFlowableSubscriber<T>
        {
            readonly IFlowableSubscriber<T> actual;

            readonly IEnumerator<IPublisher<T>> fallbacks;

            bool hasValue;

            int wip;

            internal SwitchIfEmptyEnumerableSubscriber(IFlowableSubscriber<T> actual, IEnumerator<IPublisher<T>> fallbacks)
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
                        if (source == null)
                        {
                            var fs = fallbacks;

                            bool b;
                            
                            try
                            {
                                b = fs.MoveNext();
                            }
                            catch (Exception ex)
                            {
                                fs.Dispose();
                                actual.OnError(ex);
                                return;
                            }

                            if (!b)
                            {
                                actual.OnComplete();
                                return;
                            }
                            else
                            {
                                fs.Current.Subscribe(this);
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
