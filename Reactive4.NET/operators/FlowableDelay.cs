using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Reactive.Streams;
using System.Threading;

namespace Reactive4.NET.operators
{
    sealed class FlowableDelay<T> : AbstractFlowableOperator<T, T>
    {
        readonly TimeSpan delay;

        readonly IExecutorService executor;

        public FlowableDelay(IFlowable<T> source, TimeSpan delay, IExecutorService executor) : base(source)
        {
            this.delay = delay;
            this.executor = executor;
        }

        public override void Subscribe(IFlowableSubscriber<T> subscriber)
        {
            source.Subscribe(new DelaySubscriber(subscriber, delay, executor.Worker));
        }

        sealed class DelaySubscriber : IFlowableSubscriber<T>, ISubscription
        {
            readonly IFlowableSubscriber<T> actual;

            readonly TimeSpan delay;

            readonly IExecutorWorker worker;

            ISubscription upstream;

            Exception error;

            internal DelaySubscriber(IFlowableSubscriber<T> actual, TimeSpan delay, IExecutorWorker worker)
            {
                this.actual = actual;
                this.delay = delay;
                this.worker = worker;
            }

            public void Cancel()
            {
                upstream.Cancel();
                worker.Dispose();
            }

            public void OnComplete()
            {
                worker.Schedule(Terminate, delay);
            }

            public void OnError(Exception cause)
            {
                error = cause;
                worker.Schedule(Terminate, delay);
            }

            public void OnNext(T element)
            {
                T e = element;
                worker.Schedule(() =>
                {
                    actual.OnNext(e);
                }, delay);
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
                upstream.Request(n);
            }

            void Terminate()
            {
                var ex = error;
                if (ex != null)
                {
                    actual.OnError(ex);
                }
                else
                {
                    actual.OnComplete();
                }
                worker.Dispose();
            }
        }
    }
}
