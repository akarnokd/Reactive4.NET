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
    sealed class FlowableBufferBoundary<T, U, C> : AbstractFlowableOperator<T, C> where C : ICollection<T>
    {
        readonly IPublisher<U> boundary;

        readonly Func<C> bufferSupplier;

        public FlowableBufferBoundary(IFlowable<T> source, IPublisher<U> boundary, Func<C> bufferSupplier) : base(source)
        {
            this.boundary = boundary;
            this.bufferSupplier = bufferSupplier;
        }

        public override void Subscribe(IFlowableSubscriber<C> subscriber)
        {
            C buffer;

            try
            {
                buffer = bufferSupplier();
            }
            catch (Exception ex)
            {
                subscriber.OnSubscribe(EmptySubscription<C>.Instance);
                subscriber.OnError(ex);
                return;
            }

            var parent = new BufferBoundarySubscriber(subscriber, buffer, bufferSupplier);
            subscriber.OnSubscribe(parent);
            boundary.Subscribe(parent.boundary);
            source.Subscribe(parent);
        }

        sealed class BufferBoundarySubscriber : IFlowableSubscriber<T>, IQueueSubscription<C>
        {
            readonly IFlowableSubscriber<C> actual;

            readonly Func<C> bufferSupplier;

            internal readonly BoundarySubscriber boundary;

            readonly ISimpleQueue<C> queue;

            C buffer;

            bool cancelled;

            ISubscription upstream;

            long requested;

            long emitted;

            int wip;
            bool done;
            Exception error;

            bool outputFused;

            internal BufferBoundarySubscriber(IFlowableSubscriber<C> actual, C buffer, Func<C> bufferSupplier)
            {
                this.actual = actual;
                this.buffer = buffer;
                this.bufferSupplier = bufferSupplier;
                this.boundary = new BoundarySubscriber(this);
                this.queue = new SpscLinkedArrayQueue<C>(16);
            }

            public void Cancel()
            {
                Volatile.Write(ref cancelled, true);
                SubscriptionHelper.Cancel(ref upstream);
                boundary.Cancel();
                lock (this)
                {
                    buffer = default(C);
                }
            }

            public void OnComplete()
            {
                boundary.Cancel();
                lock (this)
                {
                    var b = buffer;
                    if (b == null)
                    {
                        return;
                    }
                    buffer = default(C);
                    queue.Offer(b);
                }
                Volatile.Write(ref done, true);
                Drain();
            }

            public void OnError(Exception cause)
            {
                boundary.Cancel();
                lock (this)
                {
                    buffer = default(C);
                    error = cause;
                }
                Volatile.Write(ref done, true);
                Drain();
            }

            public void OnNext(T element)
            {
                lock (this)
                {
                    buffer?.Add(element);
                }
            }

            public void OnSubscribe(ISubscription subscription)
            {
                if (SubscriptionHelper.SetOnce(ref upstream, subscription))
                {
                    subscription.Request(long.MaxValue);
                }
            }

            public void Request(long n)
            {
                if (SubscriptionHelper.Validate(n))
                {
                    SubscriptionHelper.AddRequest(ref requested, n);
                    boundary.Request(n);
                    Drain();
                }
            }

            void Drain()
            {
                if (outputFused)
                {
                    SubscriptionHelper.QueueDrainFused(actual, ref wip, queue, ref cancelled, ref done, ref error);
                }
                else
                {
                    SubscriptionHelper.QueueDrain(actual, ref wip, queue, ref requested, ref emitted, ref cancelled, ref done, ref error);
                }
            }

            void BoundaryNext()
            {
                C u;

                try
                {
                    u = bufferSupplier();
                }
                catch (Exception ex)
                {
                    boundary.Cancel();
                    OnError(ex);
                    return;
                }

                lock (this)
                {
                    var b = buffer;
                    if (b == null)
                    {
                        return;
                    }
                    buffer = u;
                    queue.Offer(b);
                }
                Drain();
            }

            void BoundaryError(Exception cause)
            {
                SubscriptionHelper.Cancel(ref upstream);
                lock (this)
                {
                    buffer = default(C);
                    error = cause;
                }
                Volatile.Write(ref done, true);
                Drain();
            }

            void BoundaryComplete()
            {
                SubscriptionHelper.Cancel(ref upstream);
                lock (this)
                {
                    var b = buffer;
                    if (b == null)
                    {
                        return;
                    }
                    buffer = default(C);
                    queue.Offer(b);
                }
                Volatile.Write(ref done, true);
                Drain();
            }

            public int RequestFusion(int mode)
            {
                if ((mode & FusionSupport.ASYNC) != 0)
                {
                    outputFused = true;
                    return FusionSupport.ASYNC;
                }
                return FusionSupport.SYNC;
            }

            public bool Offer(C item)
            {
                throw new InvalidOperationException("Should not be called!");
            }

            public bool Poll(out C item)
            {
                return queue.Poll(out item);
            }

            public bool IsEmpty()
            {
                return queue.IsEmpty();
            }

            public void Clear()
            {
                queue.Clear();
            }

            internal class BoundarySubscriber : IFlowableSubscriber<U>, ISubscription
            {
                readonly BufferBoundarySubscriber parent;

                ISubscription upstream;

                long requested;

                internal BoundarySubscriber(BufferBoundarySubscriber parent)
                {
                    this.parent = parent;
                }

                public void Cancel()
                {
                    SubscriptionHelper.Cancel(ref upstream);
                }

                public void OnComplete()
                {
                    parent.BoundaryComplete();
                }

                public void OnError(Exception cause)
                {
                    parent.BoundaryError(cause);
                }

                public void OnNext(U element)
                {
                    parent.BoundaryNext();
                }

                public void OnSubscribe(ISubscription subscription)
                {
                    SubscriptionHelper.DeferredSetOnce(ref upstream, ref requested, subscription);
                }

                public void Request(long n)
                {
                    SubscriptionHelper.DeferredRequest(ref upstream, ref requested, n);
                }
            }
        }
    }
}
