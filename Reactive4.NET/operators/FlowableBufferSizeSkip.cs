using Reactive.Streams;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Reactive4.NET.operators
{
    sealed class FlowableBufferSizeSkip<T, C> : AbstractFlowableOperator<T, C> where C : ICollection<T>
    {
        readonly int size;

        readonly int skip;

        readonly Func<C> collectionSupplier;

        public FlowableBufferSizeSkip(IFlowable<T> source, int size, int skip, Func<C> collectionSupplier) : base(source)
        {
            this.size = size;
            this.skip = skip;
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
            source.Subscribe(new BufferSizeExactSubscriber(subscriber, buffer, collectionSupplier, size, skip));
        }

        sealed class BufferSizeExactSubscriber : IFlowableSubscriber<T>, ISubscription
        {
            readonly IFlowableSubscriber<C> actual;

            readonly Func<C> collectionSupplier;

            readonly int size;

            readonly int skip;

            int count;

            C buffer;

            ISubscription upstream;

            int once;

            internal BufferSizeExactSubscriber(IFlowableSubscriber<C> actual, C buffer, Func<C> collectionSupplier, int size, int skip)
            {
                this.actual = actual;
                this.buffer = buffer;
                this.collectionSupplier = collectionSupplier;
                this.size = size;
                this.skip = skip;
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
                int c = count + 1;

                if (c <= size)
                {
                    b.Add(element);
                }
                if (c == size)
                {
                    actual.OnNext(b);

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
                if (c == skip)
                {
                    count = 0;
                } else
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
                long u = SubscriptionHelper.MultiplyCap(n, skip);
                if (Volatile.Read(ref once) == 0 && Interlocked.CompareExchange(ref once, 1, 0) == 0)
                {
                    if (u != long.MaxValue)
                    {
                        u -= (skip - size);
                    }
                }
                upstream.Request(u);
            }
        }
    }
}
