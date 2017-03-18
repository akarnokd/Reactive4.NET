using Reactive.Streams;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Reactive4.NET.operators
{
    sealed class FlowableOnBackpressureDrop<T> : AbstractFlowableOperator<T, T>
    {
        readonly Action<T> onDrop;
        
        public FlowableOnBackpressureDrop(IFlowable<T> source, Action<T> onDrop) : base(source)
        {
            this.onDrop = onDrop;
        }

        public override void Subscribe(IFlowableSubscriber<T> subscriber)
        {
            source.Subscribe(new OnBackpressureDropSubscriber(subscriber, onDrop));
        }

        sealed class OnBackpressureDropSubscriber : IFlowableSubscriber<T>, ISubscription
        {
            readonly IFlowableSubscriber<T> actual;

            readonly Action<T> onDrop;

            long requested;

            long emitted;

            ISubscription upstream;

            internal OnBackpressureDropSubscriber(IFlowableSubscriber<T> actual, Action<T> onDrop)
            {
                this.actual = actual;
                this.onDrop = onDrop;
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
                actual.OnError(cause);
            }

            public void OnNext(T element)
            {
                var e = emitted;
                if (e != Volatile.Read(ref requested))
                {
                    actual.OnNext(element);
                    emitted = e + 1;
                }
                else
                {
                    onDrop?.Invoke(element);
                }
            }

            public void OnSubscribe(ISubscription subscription)
            {
                if (SubscriptionHelper.Validate(ref upstream, subscription))
                {
                    actual.OnSubscribe(this);

                    subscription.Request(long.MaxValue);
                }
            }

            public void Request(long n)
            {
                if (SubscriptionHelper.Validate(n))
                {
                    SubscriptionHelper.AddRequest(ref requested, n);
                }
            }
        }
    }
}
