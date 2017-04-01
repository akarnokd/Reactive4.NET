using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Reactive.Streams;
using System.Threading;

namespace Reactive4.NET.operators
{
    sealed class FlowableOnErrorComplete<T> : AbstractFlowableOperator<T, T>
    {
        public FlowableOnErrorComplete(IFlowable<T> source) : base(source)
        {
        }

        public override void Subscribe(IFlowableSubscriber<T> subscriber)
        {
            source.Subscribe(new OnErrorCompleteSubscriber(subscriber));
        }

        sealed class OnErrorCompleteSubscriber : IFlowableSubscriber<T>, ISubscription
        {
            readonly IFlowableSubscriber<T> actual;

            ISubscription upstream;

            internal OnErrorCompleteSubscriber(IFlowableSubscriber<T> actual)
            {
                this.actual = actual;
            }

            public void Cancel()
            {
                upstream.Cancel();
            }

            public void OnComplete()
            {
                actual.OnComplete();
            }

            public void OnError(Exception cause)
            {
                actual.OnComplete();
            }

            public void OnNext(T element)
            {
                actual.OnNext(element);
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
                upstream.Request(n);
            }
        }
    }
}
