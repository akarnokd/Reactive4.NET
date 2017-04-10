using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Reactive.Streams;
using Reactive4.NET.utils;
using System.Threading;

namespace Reactive4.NET.operators
{
    sealed class FlowableCollect<T, C> : AbstractFlowableOperator<T, C>
    {
        readonly Func<C> collectionSupplier;

        readonly Action<C, T> collector;

        public FlowableCollect(IFlowable<T> source, Func<C> collectionSupplier, Action<C, T> collector) : base(source)
        {
            this.collectionSupplier = collectionSupplier;
            this.collector = collector;
        }

        public override void Subscribe(IFlowableSubscriber<C> subscriber)
        {
            C initial;

            try
            {
                initial = collectionSupplier();
                if (initial == null)
                {
                    throw new NullReferenceException("The initialSupplier returned a null value");
                }
            }
            catch (Exception ex)
            {
                subscriber.OnSubscribe(EmptySubscription<T>.Instance);
                subscriber.OnError(ex);
                return;
            }

            source.Subscribe(new CollectSubscriber(subscriber, initial, collector));
        }

        internal sealed class CollectSubscriber : AbstractDeferredScalarSubscription<C>, IFlowableSubscriber<T>
        {
            readonly Action<C, T> collector;

            ISubscription upstream;

            public CollectSubscriber(IFlowableSubscriber<C> actual, C initial, Action<C, T> collector) : base(actual)
            {
                this.value = initial;
                this.collector = collector;
            }

            public void OnComplete()
            {
                Complete(value);
            }

            public void OnError(Exception cause)
            {
                if (Volatile.Read(ref state) != STATE_CANCELLED)
                {
                    value = default(C);
                    Volatile.Write(ref state, STATE_CANCELLED);
                    actual.OnError(cause);
                }
            }

            public void OnNext(T element)
            {
                try
                {
                    collector(value, element);
                }
                catch (Exception ex)
                {
                    upstream.Cancel();
                    OnError(ex);
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
        }
    }
}
