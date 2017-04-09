using Reactive.Streams;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reactive4.NET.operators
{
    sealed class FlowableThrottleFirst<T> : AbstractFlowableOperator<T, T>
    {
        readonly TimeSpan delay;

        readonly IExecutorService executor;

        public FlowableThrottleFirst(IFlowable<T> source, TimeSpan delay, IExecutorService executor) : base(source)
        {
            this.delay = delay;
            this.executor = executor;
        }

        public override void Subscribe(IFlowableSubscriber<T> subscriber)
        {
            if (subscriber is IConditionalSubscriber<T> s)
            {
                source.Subscribe(new ThrottleFirstConditionalSubscriber(s, delay, executor));
            }
            else
            {
                source.Subscribe(new ThrottleFirstSubscriber(subscriber, delay, executor));
            }
        }

        sealed class ThrottleFirstSubscriber : IConditionalSubscriber<T>, ISubscription
        {
            readonly IFlowableSubscriber<T> actual;

            readonly long delay;

            readonly IExecutorService executor;

            long last;
            bool once;

            ISubscription upstream;

            internal ThrottleFirstSubscriber(IFlowableSubscriber<T> actual, TimeSpan delay, IExecutorService executor)
            {
                this.actual = actual;
                this.delay = (long)delay.TotalMilliseconds;
                this.executor = executor;
            }

            public void Cancel()
            {
                upstream.Cancel();
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
                if (!TryOnNext(element))
                {
                    upstream.Request(1);
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
                upstream.Request(n);
            }

            public bool TryOnNext(T item)
            {
                long now = executor.Now;
                if (!once)
                {
                    once = true;
                    last = now;
                    actual.OnNext(item);
                    return true;
                }

                if (last + delay <= now)
                {
                    last = now;
                    actual.OnNext(item);
                    return true;
                }
                return false;
            }
        }

        sealed class ThrottleFirstConditionalSubscriber : IConditionalSubscriber<T>, ISubscription
        {
            readonly IConditionalSubscriber<T> actual;

            readonly long delay;

            readonly IExecutorService executor;

            long last;
            bool once;

            ISubscription upstream;

            internal ThrottleFirstConditionalSubscriber(IConditionalSubscriber<T> actual, TimeSpan delay, IExecutorService executor)
            {
                this.actual = actual;
                this.delay = (long)delay.TotalMilliseconds;
                this.executor = executor;
            }

            public void Cancel()
            {
                upstream.Cancel();
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
                if (!TryOnNext(element))
                {
                    upstream.Request(1);
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
                upstream.Request(n);
            }

            public bool TryOnNext(T item)
            {
                long now = executor.Now;
                if (!once)
                {
                    once = true;
                    last = now;
                    return actual.TryOnNext(item);
                }

                if (last + delay <= now)
                {
                    last = now;
                    return actual.TryOnNext(item);
                }
                return false;
            }
        }
    }
}
