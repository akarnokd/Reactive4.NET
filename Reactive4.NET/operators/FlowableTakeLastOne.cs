using Reactive4.NET.utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Reactive.Streams;

namespace Reactive4.NET.operators
{
    sealed class FlowableTakeLastOne<T> : AbstractFlowableOperator<T, T>
    {
        public FlowableTakeLastOne(IFlowable<T> source) : base(source)
        {
        }

        public override void Subscribe(IFlowableSubscriber<T> subscriber)
        {
            source.Subscribe(new TakeLastOneSubscriber(subscriber));
        }

        sealed class TakeLastOneSubscriber : AbstractDeferredScalarSubscription<T>, IFlowableSubscriber<T>
        {
            ISubscription upstream;

            bool hasLast;

            public TakeLastOneSubscriber(IFlowableSubscriber<T> actual) : base(actual)
            {
            }

            public void OnComplete()
            {
                if (hasLast)
                {
                    Complete(value);
                }
                else
                {
                    Complete();
                }
            }

            public void OnError(Exception cause)
            {
                value = default(T);
                Error(cause);
            }

            public void OnNext(T element)
            {
                value = element;
                if (!hasLast)
                {
                    hasLast = true;
                }
            }

            public void OnSubscribe(ISubscription subscription)
            {
                if (SubscriptionHelper.Validate(ref upstream, subscription))
                {
                    actual.OnSubscribe(this);

                    upstream.Request(long.MaxValue);
                }
            }
        }
    }
}
