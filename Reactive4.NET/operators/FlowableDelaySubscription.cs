using Reactive.Streams;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reactive4.NET.operators
{
    sealed class FlowableDelaySubscription<T, U> : AbstractFlowableOperator<T, T>
    {
        readonly IPublisher<U> other;

        public FlowableDelaySubscription(IFlowable<T> source, IPublisher<U> other) : base(source)
        {
            this.other = other;
        }

        public override void Subscribe(IFlowableSubscriber<T> subscriber)
        {
            var o = new OtherSubscriber(subscriber, source);
            subscriber.OnSubscribe(o);
            other.Subscribe(o);
        }

        sealed class OtherSubscriber : ISubscriber<U>, ISubscription
        {
            readonly IFlowableSubscriber<T> actual;

            readonly IFlowable<T> source;

            ISubscription upstream;

            ISubscription mainUpstream;

            long requested;

            bool done;

            internal OtherSubscriber(IFlowableSubscriber<T> actual, IFlowable<T> source)
            {
                this.actual = actual;
                this.source = source;
            }

            public void Cancel()
            {
                SubscriptionHelper.Cancel(ref upstream);
                SubscriptionHelper.Cancel(ref mainUpstream);
            }

            public void OnComplete()
            {
                if (!done)
                {
                    done = true;

                    source.Subscribe(new MainSubscriber(this));
                }
            }

            public void OnError(Exception cause)
            {
                if (!done)
                {
                    actual.OnError(cause);
                }
            }

            public void OnNext(U element)
            {
                upstream.Cancel();
                OnComplete();
            }

            public void OnSubscribe(ISubscription subscription)
            {
                if (SubscriptionHelper.SetOnce(ref upstream, subscription))
                {
                    upstream.Request(long.MaxValue);
                }
            }

            public void Request(long n)
            {
                SubscriptionHelper.DeferredRequest(ref mainUpstream, ref requested, n);
            }

            internal void MainSubscribe(ISubscription subscription)
            {
                SubscriptionHelper.DeferredSetOnce(ref mainUpstream, ref requested, subscription);
            }

            sealed class MainSubscriber : ISubscriber<T>
            {
                readonly OtherSubscriber parent;

                readonly ISubscriber<T> actual;

                internal MainSubscriber(OtherSubscriber parent)
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
                    parent.MainSubscribe(subscription);
                }
            }
        }
    }
}
