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
    sealed class FlowableTimeout<T> : AbstractFlowableOperator<T, T>
    {
        readonly TimeSpan firstTimeout;

        readonly TimeSpan itemTimeout;

        readonly IExecutorService executor;

        readonly IPublisher<T> fallback;

        public FlowableTimeout(IFlowable<T> source, TimeSpan firstTimeout, TimeSpan itemTimeout, IExecutorService executor, IPublisher<T> fallback) : base(source)
        {
            this.firstTimeout = firstTimeout;
            this.itemTimeout = itemTimeout;
            this.executor = executor;
            this.fallback = fallback;
        }

        public override void Subscribe(IFlowableSubscriber<T> subscriber)
        {
            source.Subscribe(new TimeoutSubscriber(subscriber, firstTimeout, itemTimeout, executor.Worker, fallback));
        }

        sealed class TimeoutSubscriber : SubscriptionArbiter, IFlowableSubscriber<T>
        {
            readonly IFlowableSubscriber<T> actual;

            readonly TimeSpan firstTimeout;

            readonly TimeSpan itemTimeout;

            readonly IExecutorWorker worker;

            readonly IPublisher<T> fallback;

            ISubscription upstream;

            IDisposable timer;

            long index;

            internal TimeoutSubscriber(IFlowableSubscriber<T> actual, TimeSpan firstTimeout,
                TimeSpan itemTimeout, IExecutorWorker worker, IPublisher<T> fallback)
            {
                this.actual = actual;
                this.firstTimeout = firstTimeout;
                this.itemTimeout = itemTimeout;
                this.worker = worker;
                this.fallback = fallback;
            }

            public void OnComplete()
            {
                if (Interlocked.Exchange(ref index, long.MaxValue) != long.MaxValue)
                {
                    DisposableHelper.Dispose(ref timer);
                    actual.OnComplete();
                    worker.Dispose();
                }
            }

            public void OnError(Exception cause)
            {
                if (Interlocked.Exchange(ref index, long.MaxValue) != long.MaxValue)
                {
                    DisposableHelper.Dispose(ref timer);
                    actual.OnError(cause);
                    worker.Dispose();
                }
            }

            public void OnNext(T element)
            {
                Volatile.Read(ref timer)?.Dispose();

                long idx = Volatile.Read(ref index);
                if (Interlocked.CompareExchange(ref index, idx + 1, idx) == idx)
                {
                    actual.OnNext(element);

                    DisposableHelper.Replace(ref timer, worker.Schedule(() => Timeout(idx + 1), itemTimeout));
                }
            }

            public void OnSubscribe(ISubscription subscription)
            {
                if (SubscriptionHelper.Validate(ref upstream, subscription))
                {
                    SequentialDisposable sd = new SequentialDisposable();
                    timer = sd;

                    ArbiterSet(subscription);

                    actual.OnSubscribe(this);

                    sd.Set(worker.Schedule(() => Timeout(0), firstTimeout));
                }
            }

            public override void Cancel()
            {
                Interlocked.Exchange(ref index, long.MaxValue);
                upstream = SubscriptionHelper.Cancelled;
                base.Cancel();
                DisposableHelper.Dispose(ref timer);
                worker.Dispose();
            }

            void Timeout(long idx)
            {
                if (Volatile.Read(ref index) == idx && Interlocked.CompareExchange(ref index, long.MaxValue, idx) == idx)
                {
                    SubscriptionHelper.Cancel(ref upstream);
                    Interlocked.Exchange(ref timer, DisposableHelper.Disposed);

                    var p = fallback;

                    if (p == null)
                    {
                        actual.OnError(new TimeoutException());
                    }
                    else
                    {
                        p.Subscribe(new FallbackSubscriber(this));
                    }
                    worker.Dispose();
                }
            }

            sealed class FallbackSubscriber : IFlowableSubscriber<T>
            {
                readonly IFlowableSubscriber<T> actual;

                readonly SubscriptionArbiter arbiter;

                internal FallbackSubscriber(TimeoutSubscriber parent)
                {
                    this.actual = parent.actual;
                    this.arbiter = parent;
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
                    arbiter.ArbiterSet(subscription);
                }
            }
        }
    }
}
