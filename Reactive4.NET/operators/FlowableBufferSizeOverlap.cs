using Reactive.Streams;
using Reactive4.NET.utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Reactive4.NET.operators
{
    sealed class FlowableBufferSizeOverlap<T, C> : AbstractFlowableOperator<T, C> where C : ICollection<T>
    {
        readonly int size;

        readonly int skip;

        readonly Func<C> collectionSupplier;

        public FlowableBufferSizeOverlap(IFlowable<T> source, int size, int skip, Func<C> collectionSupplier) : base(source)
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

            readonly ArrayQueue<C> queue;

            readonly Action<T, C> onAdd;

            int count;
            int index;

            ISubscription upstream;

            int firstRequest;

            bool done;
            bool cancelled;

            long requested;
            long produced;

            internal BufferSizeExactSubscriber(IFlowableSubscriber<C> actual, C buffer, Func<C> collectionSupplier, int size, int skip)
            {
                this.actual = actual;
                this.queue = new ArrayQueue<C>();
                this.queue.Offer(buffer);
                this.collectionSupplier = collectionSupplier;
                this.size = size;
                this.skip = skip;
                this.onAdd = this.AddTo;
            }

            public void Cancel()
            {
                Volatile.Write(ref cancelled, true);
                upstream.Cancel();
            }

            public void OnComplete()
            {
                if (done)
                {
                    return;
                }
                done = true;
                if (count == 0)
                {
                    actual.OnComplete();
                    return;
                }
                long p = produced;
                if (p != 0L)
                {
                    SubscriptionHelper.Produced(ref requested, p);
                }
                SubscriptionHelper.PostCompleteMultiResult(actual, ref requested, queue, ref cancelled);
            }

            public void OnError(Exception cause)
            {
                if (done)
                {
                    return;
                }
                done = true;
                queue.Clear();
                actual.OnError(cause);
            }

            public void OnNext(T element)
            {
                if (done)
                {
                    return;
                }

                int idx = index;
                if (idx == skip)
                {
                    C b;

                    try
                    {
                        b = collectionSupplier();
                    }
                    catch (Exception ex)
                    {
                        upstream.Cancel();
                        OnError(ex);
                        return;
                    }

                    queue.Offer(b);
                    index = 1;
                } else
                {
                    index = idx + 1;
                }
                queue.ForEach<T>(element, onAdd);
                int c = count + 1;
                if (c == size)
                {
                    queue.Poll(out C b);
                    actual.OnNext(b);
                    count = size - skip;
                    produced++;
                }
                else
                {
                    count = c;
                }
            }

            void AddTo(T item, C buffer)
            {
                buffer.Add(item);
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
                if (!SubscriptionHelper.PostCompleteMultiRequest(actual, ref requested, queue, n, ref cancelled))
                {
                    long u = SubscriptionHelper.MultiplyCap(n, size - skip);
                    if (Volatile.Read(ref firstRequest) == 0 && Interlocked.CompareExchange(ref firstRequest, 1, 0) == 0)
                    {
                        u += skip;
                        if (u < 0L)
                        {
                            u = long.MaxValue;
                        }
                    }
                    upstream.Request(u);
                }
            }
        }
    }
}
