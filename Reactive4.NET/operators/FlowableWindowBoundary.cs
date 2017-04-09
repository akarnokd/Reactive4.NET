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
    sealed class FlowableWindowBoundary<T, U> : AbstractFlowableOperator<T, IFlowable<T>>
    {
        readonly IPublisher<U> boundary;

        readonly int bufferSize;

        public FlowableWindowBoundary(IFlowable<T> source, IPublisher<U> boundary, int bufferSize) : base(source)
        {
            this.boundary = boundary;
            this.bufferSize = bufferSize;
        }

        public override void Subscribe(IFlowableSubscriber<IFlowable<T>> subscriber)
        {
            var parent = new WindowBoundarySubscriber(subscriber, bufferSize);
            subscriber.OnSubscribe(parent);
            boundary.Subscribe(parent.boundary);
            source.Subscribe(parent);
        }

        sealed class WindowBoundarySubscriber : IFlowableSubscriber<T>, IQueueSubscription<IFlowable<T>>
        {
            readonly IFlowableSubscriber<IFlowable<T>> actual;

            readonly ISimpleQueue<IFlowable<T>> queue;

            internal readonly BoundarySubscriber boundary;

            readonly int bufferSize;

            readonly Action terminate;

            UnicastProcessor<T> buffer;

            bool cancelled;

            ISubscription upstream;

            long requested;

            long emitted;

            int wip;
            bool done;
            Exception error;

            bool outputFused;

            int active;
            int once;

            internal WindowBoundarySubscriber(IFlowableSubscriber<IFlowable<T>> actual, int bufferSize)
            {
                this.actual = actual;
                this.active = 1;
                this.queue = new SpscLinkedArrayQueue<IFlowable<T>>(16);
                this.terminate = OnTerminate;
                this.bufferSize = bufferSize;
                this.buffer = new UnicastProcessor<T>(bufferSize, terminate);
                this.boundary = new BoundarySubscriber(this);
                queue.Offer(buffer);
            }

            void OnTerminate()
            {
                if (Interlocked.Decrement(ref active) == 0)
                {
                    upstream.Cancel();
                }
            }

            public void Cancel()
            {
                Volatile.Write(ref cancelled, true);
                boundary.Cancel();
                if (Interlocked.CompareExchange(ref once, 1, 0) == 0)
                {
                    OnTerminate();
                }
            }

            public void OnComplete()
            {
                boundary.Cancel();
                UnicastProcessor<T> b;
                lock (this)
                {
                    b = buffer;
                    if (b == null)
                    {
                        return;
                    }
                    buffer = null;
                }
                b.OnComplete();
                Volatile.Write(ref done, true);
                Drain();
            }

            public void OnError(Exception cause)
            {
                boundary.Cancel();
                UnicastProcessor<T> b;
                lock (this)
                {
                    b = buffer;
                    if (b == null)
                    {
                        return;
                    }
                    buffer = null;
                    error = cause;
                }
                b.OnError(cause);
                Volatile.Write(ref done, true);
                Drain();
            }

            public void OnNext(T element)
            {
                lock (this)
                {
                    buffer?.OnNext(element);
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

            public int RequestFusion(int mode)
            {
                if ((mode & FusionSupport.ASYNC) != 0)
                {
                    outputFused = true;
                    return FusionSupport.ASYNC;
                }
                return FusionSupport.SYNC;
            }

            public bool Offer(IFlowable<T> item)
            {
                throw new InvalidOperationException("Should not be called!");
            }

            public bool Poll(out IFlowable<T> item)
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

            void BoundaryNext()
            {
                int a = Volatile.Read(ref active);
                if (a != 0 && Interlocked.CompareExchange(ref active, a + 1, a) == a)
                {
                    var u = new UnicastProcessor<T>(bufferSize, terminate);
                    UnicastProcessor<T> b;
                    lock (this)
                    {
                        b = buffer;
                        if (b == null)
                        {
                            return;
                        }
                        buffer = u;
                        queue.Offer(u);
                    }
                    b.OnComplete();
                    Drain();
                }
            }

            void BoundaryError(Exception cause)
            {
                SubscriptionHelper.Cancel(ref upstream);
                UnicastProcessor<T> b;
                lock (this)
                {
                    b = buffer;
                    if (b == null)
                    {
                        return;
                    }
                    buffer = null;
                    error = cause;
                }
                b.OnError(cause);
                Volatile.Write(ref done, true);
                Drain();
            }

            void BoundaryComplete()
            {
                SubscriptionHelper.Cancel(ref upstream);
                UnicastProcessor<T> b;
                lock (this)
                {
                    b = buffer;
                    if (b == null)
                    {
                        return;
                    }
                    buffer = null;
                }
                b.OnComplete();
                Volatile.Write(ref done, true);
                Drain();
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

            internal class BoundarySubscriber : IFlowableSubscriber<U>, ISubscription
            {
                readonly WindowBoundarySubscriber parent;

                ISubscription upstream;

                long requested;

                internal BoundarySubscriber(WindowBoundarySubscriber parent)
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
