using Reactive.Streams;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reactive4.NET.operators
{
    sealed class FlowableBufferSizeExact<T, C> : AbstractFlowableOperator<T, C> where C : ICollection<T>
    {
        readonly int size;

        readonly Func<C> collectionSupplier;

        public FlowableBufferSizeExact(IFlowable<T> source, int size, Func<C> collectionSupplier) : base(source)
        {
            this.size = size;
            this.collectionSupplier = collectionSupplier;
        }

        public override void Subscribe(IFlowableSubscriber<C> subscriber)
        {
            C buffer;

            try
            {
                buffer = collectionSupplier();
            }
            catch (Exception ex)
            {
                subscriber.OnSubscribe(EmptySubscription<C>.Instance);
                subscriber.OnError(ex);
                return;
            }
            source.Subscribe(new BufferSizeExactSubscriber(subscriber, buffer, collectionSupplier, size));
        }

        sealed class BufferSizeExactSubscriber : IFlowableSubscriber<T>, ISubscription
        {
            readonly IFlowableSubscriber<C> actual;

            readonly Func<C> collectionSupplier;

            readonly int size;

            int count;

            C buffer;

            ISubscription upstream;

            internal BufferSizeExactSubscriber(IFlowableSubscriber<C> actual, C buffer, Func<C> collectionSupplier, int size)
            {
                this.actual = actual;
                this.buffer = buffer;
                this.collectionSupplier = collectionSupplier;
                this.size = size;
            }

            public void Cancel()
            {
                upstream.Cancel();
            }

            public void OnComplete()
            {
                var b = buffer;
                if (b == null)
                {
                    return;
                }
                buffer = default(C);
                if (count != 0)
                {
                    actual.OnNext(b);
                }
                actual.OnComplete();
            }

            public void OnError(Exception cause)
            {
                var b = buffer;
                if (b == null)
                {
                    return;
                }
                buffer = default(C);
                actual.OnError(cause);
            }

            public void OnNext(T element)
            {
                var b = buffer;
                if (b == null)
                {
                    return;
                }

                b.Add(element);

                var c = count + 1;

                if (c == size)
                {
                    actual.OnNext(b);

                    count = 0;

                    try
                    {
                        buffer = collectionSupplier();
                    }
                    catch (Exception ex)
                    {
                        upstream.Cancel();
                        OnError(ex);
                        return;
                    }
                }
                else
                {
                    count = c;
                }
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
                long u = SubscriptionHelper.MultiplyCap(n, size);
                upstream.Request(u);
            }
        }
    }
}
