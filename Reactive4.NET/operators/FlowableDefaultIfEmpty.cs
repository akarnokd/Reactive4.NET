using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Reactive.Streams;
using System.Threading;

namespace Reactive4.NET.operators
{
    sealed class FlowableDefaultIfEmpty<T> : AbstractFlowableOperator<T, T>
    {
        readonly T defaultItem;

        public FlowableDefaultIfEmpty(IFlowable<T> source, T defaultItem) : base(source)
        {
            this.defaultItem = defaultItem;
        }

        public override void Subscribe(IFlowableSubscriber<T> subscriber)
        {
            source.Subscribe(new DefaultIfEmptySubscriber(subscriber, defaultItem));
        }

        sealed class DefaultIfEmptySubscriber : IFlowableSubscriber<T>, ISubscription
        {
            readonly IFlowableSubscriber<T> actual;

            T defaultItem;

            ISubscription upstream;

            long requested;

            bool hasValue;

            bool cancelled;

            internal DefaultIfEmptySubscriber(IFlowableSubscriber<T> actual, T defaultItem)
            {
                this.actual = actual;
                this.defaultItem = defaultItem;
            }

            public void Cancel()
            {
                Volatile.Write(ref cancelled, true);
                upstream.Cancel();
            }

            public void OnComplete()
            {
                if (hasValue)
                {
                    actual.OnComplete();
                }
                else
                {
                    SubscriptionHelper.PostCompleteSingleResult(actual, ref requested, ref defaultItem, defaultItem, ref cancelled);
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
                if (SubscriptionHelper.Validate(ref upstream, subscription))
                {
                    actual.OnSubscribe(this);
                }
            }

            public void Request(long n)
            {
                if (!SubscriptionHelper.PostCompleteSingleRequest(actual, ref requested, ref defaultItem, n, ref cancelled))
                {
                    upstream.Request(n);
                }
            }
        }
    }
}
