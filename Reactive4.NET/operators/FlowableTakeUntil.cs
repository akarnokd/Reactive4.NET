using Reactive.Streams;
using Reactive4.NET.utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reactive4.NET.operators
{
    sealed class FlowableTakeUntil<T, U> : AbstractFlowableOperator<T, T>
    {
        readonly IPublisher<U> other;

        public FlowableTakeUntil(IFlowable<T> source, IPublisher<U> other) : base(source)
        {
            this.other = other;
        }

        public override void Subscribe(IFlowableSubscriber<T> subscriber)
        {
            var parent = new TakeUntilSubscriber(subscriber);
            subscriber.OnSubscribe(parent);

            other.Subscribe(parent.other);
            source.Subscribe(parent);
        }

        sealed class TakeUntilSubscriber : IFlowableSubscriber<T>, ISubscription
        {
            readonly IFlowableSubscriber<T> actual;

            internal readonly OtherSubscriber other;

            ISubscription upstream;

            long requested;

            int wip;

            Exception error;

            internal TakeUntilSubscriber(IFlowableSubscriber<T> actual)
            {
                this.actual = actual;
                this.other = new OtherSubscriber(this);
            }

            public void Cancel()
            {
                SubscriptionHelper.Cancel(ref other.upstream);
                SubscriptionHelper.Cancel(ref upstream);
            }

            public void OnComplete()
            {
                SubscriptionHelper.Cancel(ref other.upstream);
                SerializationHelper.OnComplete(actual, ref wip, ref error);
            }

            public void OnError(Exception cause)
            {
                SubscriptionHelper.Cancel(ref other.upstream);
                SerializationHelper.OnError(actual, ref wip, ref error, cause);
            }

            public void OnNext(T element)
            {
                SerializationHelper.OnNext(actual, ref wip, ref error, element);
            }

            public void OnSubscribe(ISubscription subscription)
            {
                SubscriptionHelper.DeferredSetOnce(ref upstream, ref requested, subscription);
            }

            public void Request(long n)
            {
                SubscriptionHelper.DeferredRequest(ref upstream, ref requested, n);
            }

            void OtherSignal()
            {
                SubscriptionHelper.Cancel(ref upstream);
                SerializationHelper.OnComplete(actual, ref wip, ref error);
            }

            void OtherError(Exception ex)
            {
                SubscriptionHelper.Cancel(ref upstream);
                SerializationHelper.OnError(actual, ref wip, ref error, ex);
            }

            internal sealed class OtherSubscriber : IFlowableSubscriber<U>
            {
                readonly TakeUntilSubscriber parent;

                bool done;

                internal ISubscription upstream;

                internal OtherSubscriber(TakeUntilSubscriber parent)
                {
                    this.parent = parent;
                }

                public void OnComplete()
                {
                    if (!done)
                    {
                        done = true;
                        parent.OtherSignal();
                    }
                }

                public void OnError(Exception cause)
                {
                    if (!done)
                    {
                        done = true;
                        parent.OtherError(cause);
                    }
                }

                public void OnNext(U element)
                {
                    if (!done)
                    {
                        upstream.Cancel();
                        done = true;
                        parent.OtherSignal();
                    }
                }

                public void OnSubscribe(ISubscription subscription)
                {
                    if (SubscriptionHelper.SetOnce(ref upstream, subscription))
                    {
                        subscription.Request(long.MaxValue);
                    }
                }
            }
        }
    }
}
