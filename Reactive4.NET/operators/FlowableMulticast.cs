using Reactive.Streams;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reactive4.NET.operators
{
    sealed class FlowableMulticast<T, R> : AbstractFlowableOperator<T, R>
    {
        readonly Func<IFlowableProcessor<T>> processorSupplier;

        readonly Func<IFlowable<T>, IPublisher<R>> handler;

        public FlowableMulticast(IFlowable<T> source, Func<IFlowable<T>, IPublisher<R>> handler, Func<IFlowableProcessor<T>> processorSupplier) : base(source)
        {
            this.handler = handler;
            this.processorSupplier = processorSupplier;
        }

        public override void Subscribe(IFlowableSubscriber<R> subscriber)
        {
            IFlowableProcessor<T> processor;
            IPublisher<R> publisher;

            try
            {
                processor = processorSupplier();
                publisher = handler(processor);
            }
            catch (Exception ex)
            {
                subscriber.OnSubscribe(EmptySubscription<R>.Instance);
                subscriber.OnError(ex);
                return;
            }

            publisher.Subscribe(new MulticastOnCancel(subscriber, processor));

            source.Subscribe(processor);
        }

        sealed class MulticastOnCancel : IFlowableSubscriber<R>, ISubscription
        {
            readonly IFlowableSubscriber<R> actual;

            readonly IFlowableProcessor<T> processor;

            ISubscription upstream;

            internal MulticastOnCancel(IFlowableSubscriber<R> actual, IFlowableProcessor<T> processor)
            {
                this.actual = actual;
                this.processor = processor;
            }

            public void Cancel()
            {
                upstream.Cancel();
                processor.Dispose();
            }

            public void OnComplete()
            {
                processor.Dispose();
                actual.OnComplete();
            }

            public void OnError(Exception cause)
            {
                processor.Dispose();
                actual.OnError(cause);
            }

            public void OnNext(R element)
            {
                actual.OnNext(element);
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
        }
    }
}
