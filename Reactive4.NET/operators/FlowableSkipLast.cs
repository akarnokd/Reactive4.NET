using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Reactive.Streams;
using Reactive4.NET.utils;

namespace Reactive4.NET.operators
{
    sealed class FlowableSkipLast<T> : AbstractFlowableOperator<T, T>
    {
        readonly int n;

        public FlowableSkipLast(IFlowable<T> source, int n) : base(source)
        {
            this.n = n;
        }

        public override void Subscribe(IFlowableSubscriber<T> subscriber)
        {
            source.Subscribe(new SkipLastSubscriber(subscriber, n));
        }

        sealed class SkipLastSubscriber : IFlowableSubscriber<T>, ISubscription
        {
            readonly IFlowableSubscriber<T> actual;

            readonly int n;

            readonly ArrayQueue<T> queue;

            ISubscription upstream;

            internal SkipLastSubscriber(IFlowableSubscriber<T> actual, int n)
            {
                this.actual = actual;
                this.n = n;
                this.queue = new ArrayQueue<T>();
            }

            public void Cancel()
            {
                upstream.Cancel();
            }

            public void OnComplete()
            {
                queue.Clear();
                actual.OnComplete();
            }

            public void OnError(Exception cause)
            {
                queue.Clear();
                actual.OnError(cause);
            }

            public void OnNext(T element)
            {
                var q = queue;
                if (q.Count == n)
                {
                    q.Poll(out T v);

                    actual.OnNext(v);
                }
                q.Offer(element);
            }

            public void OnSubscribe(ISubscription subscription)
            {
                if (SubscriptionHelper.Validate(ref upstream, subscription))
                {
                    actual.OnSubscribe(this);

                    subscription.Request(n);
                }
            }

            public void Request(long n)
            {
                upstream.Request(n);
            }
        }
    }
}
