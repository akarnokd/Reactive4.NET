using Reactive.Streams;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Reactive4.NET.operators
{
    sealed class FlowableOnBackpressureError<T> : AbstractFlowableOperator<T, T>
    {
        public FlowableOnBackpressureError(IFlowable<T> source) : base(source)
        {
        }

        public override void Subscribe(IFlowableSubscriber<T> subscriber)
        {
            source.Subscribe(new OnBackpressureErrorSubscriber(subscriber));
        }

        sealed class OnBackpressureErrorSubscriber : IFlowableSubscriber<T>, ISubscription
        {
            readonly IFlowableSubscriber<T> actual;

            long requested;

            long emitted;

            ISubscription upstream;

            bool done;

            internal OnBackpressureErrorSubscriber(IFlowableSubscriber<T> actual)
            {
                this.actual = actual;
            }

            public void Cancel()
            {
                upstream.Cancel();
            }

            public void OnComplete()
            {
                if (!done)
                {
                    done = true;
                    actual.OnComplete();
                }
            }

            public void OnError(Exception cause)
            {
                if (!done)
                {
                    done = true;
                    actual.OnError(cause);
                }
            }

            public void OnNext(T element)
            {
                if (!done)
                {
                    var e = emitted;
                    if (e != Volatile.Read(ref requested))
                    {
                        actual.OnNext(element);
                        emitted = e + 1;
                    }
                    else
                    {
                        upstream.Cancel();
                        OnError(new InvalidOperationException("Could not emit value due to lack of requests."));
                    }
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
