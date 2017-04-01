using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Reactive.Streams;
using System.Threading;

namespace Reactive4.NET.operators
{
    sealed class FlowableOnErrorReturn<T> : AbstractFlowableOperator<T, T>
    {
        readonly T fallback;

        public FlowableOnErrorReturn(IFlowable<T> source, T fallback) : base(source)
        {
            this.fallback = fallback;
        }

        public override void Subscribe(IFlowableSubscriber<T> subscriber)
        {
            source.Subscribe(new OnErrorReturnSubscriber(subscriber, fallback));
        }

        sealed class OnErrorReturnSubscriber : IFlowableSubscriber<T>, ISubscription
        {
            readonly IFlowableSubscriber<T> actual;

            T fallback;

            ISubscription upstream;
            long requested;

            bool cancelled;

            long produced;

            internal OnErrorReturnSubscriber(IFlowableSubscriber<T> actual, T fallback)
            {
                this.actual = actual;
                this.fallback = fallback;
            }

            public void Cancel()
            {
                Volatile.Write(ref cancelled, true);
                upstream.Cancel();
            }

            public void OnComplete()
            {
                actual.OnComplete();
            }

            public void OnError(Exception cause)
            {
                long p = produced;
                if (p != 0L)
                {
                    SubscriptionHelper.Produced(ref requested, p);
                }
                SubscriptionHelper.PostCompleteSingleResult(actual, ref requested, ref fallback, fallback, ref cancelled);
            }

            public void OnNext(T element)
            {
                produced++;
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
                if (!SubscriptionHelper.PostCompleteSingleRequest(actual, ref requested, ref fallback, n, ref cancelled))
                {
                    upstream.Request(n);
                }
            }
        }
    }
}
