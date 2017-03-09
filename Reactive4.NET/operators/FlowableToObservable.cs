using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Reactive.Streams;

namespace Reactive4.NET.operators
{
    sealed class FlowableToObservable<T> : IObservable<T>
    {
        readonly IFlowable<T> source;

        internal FlowableToObservable(IFlowable<T> source)
        {
            this.source = source;
        }

        public IDisposable Subscribe(IObserver<T> observer)
        {
            var o = new ToObservableSubscriber(observer);
            source.Subscribe(o);
            return o;
        }

        sealed class ToObservableSubscriber : IFlowableSubscriber<T>, IDisposable
        {
            readonly IObserver<T> actual;

            ISubscription upstream;

            internal ToObservableSubscriber(IObserver<T> actual)
            {
                this.actual = actual;
            }

            public void Dispose()
            {
                SubscriptionHelper.Cancel(ref upstream);
            }

            public void OnComplete()
            {
                actual.OnCompleted();
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
                if (SubscriptionHelper.SetOnce(ref upstream, subscription))
                {
                    subscription.Request(long.MaxValue);
                }
            }
        }
    }
}
