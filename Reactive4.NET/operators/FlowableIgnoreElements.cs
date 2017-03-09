using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Reactive.Streams;

namespace Reactive4.NET.operators
{
    sealed class FlowableIgnoreElements<T> : AbstractFlowableOperator<T, T>
    {
        public FlowableIgnoreElements(IFlowable<T> source) : base(source)
        {
        }

        public override void Subscribe(IFlowableSubscriber<T> subscriber)
        {
            source.Subscribe(new IgnoreElementsSubscriber(subscriber));
        }

        sealed class IgnoreElementsSubscriber : IConditionalSubscriber<T>, IQueueSubscription<T>
        {
            readonly IFlowableSubscriber<T> actual;

            ISubscription upstream;

            internal IgnoreElementsSubscriber(IFlowableSubscriber<T> actual)
            {
                this.actual = actual;
            }

            public void Cancel()
            {
                upstream.Cancel();
            }

            public void Clear()
            {
                // deliberately no op
            }

            public bool IsEmpty()
            {
                return true;
            }

            public bool Offer(T item)
            {
                throw new InvalidOperationException("Should not be called");
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
                // deliberately ignored
            }

            public void OnSubscribe(ISubscription subscription)
            {
                if (SubscriptionHelper.Validate(ref upstream, subscription))
                {
                    actual.OnSubscribe(this);

                    subscription.Request(long.MaxValue);
                }
            }

            public bool Poll(out T item)
            {
                item = default(T);
                return false;
            }

            public void Request(long n)
            {
                // deliberately ignored
            }

            public int RequestFusion(int mode)
            {
                return mode & FusionSupport.ASYNC;
            }

            public bool TryOnNext(T item)
            {
                // deliberately ignored
                return false;
            }
        }
    }
}
