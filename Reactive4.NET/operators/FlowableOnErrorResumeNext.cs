using Reactive.Streams;
using Reactive4.NET.utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reactive4.NET.operators
{
    sealed class FlowableOnErrorResumeNext<T> : AbstractFlowableOperator<T, T>
    {
        readonly Func<Exception, IPublisher<T>> handler;

        public FlowableOnErrorResumeNext(IFlowable<T> source, Func<Exception, IPublisher<T>> handler) : base(source)
        {
            this.handler = handler;
        }

        public override void Subscribe(IFlowableSubscriber<T> subscriber)
        {
            var parent = new OnErrorResumeNextSubscriber(subscriber, handler);
            subscriber.OnSubscribe(parent);
            source.Subscribe(parent);
        }

        sealed class OnErrorResumeNextSubscriber : SubscriptionArbiter, IFlowableSubscriber<T>
        {
            readonly IFlowableSubscriber<T> actual;

            readonly Func<Exception, IPublisher<T>> handler;

            readonly OtherSubscriber other;

            long produced;

            internal OnErrorResumeNextSubscriber(IFlowableSubscriber<T> actual, Func<Exception, IPublisher<T>> handler)
            {
                this.actual = actual;
                this.handler = handler;
                this.other = new OtherSubscriber(this);
            }

            public void OnComplete()
            {
                actual.OnComplete();
            }

            public void OnError(Exception cause)
            {
                IPublisher<T> p;

                try
                {
                    p = handler(cause);
                }
                catch (Exception ex)
                {
                    actual.OnError(new AggregateException(cause, ex));
                    return;
                }
                long c = produced;
                if (c != 0L)
                {
                    ArbiterProduced(c);
                }
                p.Subscribe(other);
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

            sealed class OtherSubscriber : IFlowableSubscriber<T>
            {
                readonly IFlowableSubscriber<T> actual;

                readonly OnErrorResumeNextSubscriber parent;

                internal OtherSubscriber(OnErrorResumeNextSubscriber parent)
                {
                    this.parent = parent;
                    this.actual = parent.actual;
                }

                public void OnComplete()
                {
                    actual.OnComplete();
                }

                public void OnError(Exception cause)
                {
                    actual.OnError(cause);
                }

                public void OnNext(T element)
                {
                    actual.OnNext(element);
                }

                public void OnSubscribe(ISubscription subscription)
                {
                    parent.ArbiterSet(subscription);
                }
            }
        }
    }
}
