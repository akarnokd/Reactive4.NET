using Reactive4.NET.subscribers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Reactive.Streams;

namespace Reactive4.NET.operators
{
    sealed class FlowableSkip<T> : AbstractFlowableOperator<T, T>
    {
        readonly long n;

        public FlowableSkip(IFlowable<T> source, long n) : base(source)
        {
            this.n = n;
        }

        public override void Subscribe(IFlowableSubscriber<T> subscriber)
        {
            if (subscriber is IConditionalSubscriber<T> s)
            {
                source.Subscribe(new SkipConditionalSubscriber(s, n));
            }
            else
            {
                source.Subscribe(new SkipSubscriber(subscriber, n));
            }
        }

        sealed class SkipSubscriber : AbstractFuseableSubscriber<T, T>
        {
            readonly IFlowableSubscriber<T> actual;

            long remaining;

            internal SkipSubscriber(IFlowableSubscriber<T> actual, long n)
            {
                this.actual = actual;
                this.remaining = n;
            }

            public override void OnComplete()
            {
                actual.OnComplete();
            }

            public override void OnError(Exception cause)
            {
                actual.OnError(cause);
            }

            public override void OnNext(T element)
            {
                long n = remaining;
                if (n > 0)
                {
                    remaining = n - 1;
                    return;
                }
                actual.OnNext(element);
            }

            public override bool Poll(out T item)
            {
                long n = remaining;
                if (n > 0)
                {
                    while (n > 0)
                    {
                        if (!qs.Poll(out T v))
                        {
                            remaining = n;
                            item = default(T);
                            return false;
                        }
                        n--;
                    }
                    remaining = 0;
                }
                return qs.Poll(out item);
            }

            protected override void OnStart(ISubscription subscription)
            {
                long n = remaining;
                actual.OnSubscribe(this);
                if (n > 0 && fusionMode != FusionSupport.SYNC)
                {
                    subscription.Request(n);
                }
            }

            public override int RequestFusion(int mode)
            {
                return base.RequestTransientFusion(mode);
            }
        }

        sealed class SkipConditionalSubscriber : AbstractFuseableConditionalSubscriber<T, T>
        {
            readonly IConditionalSubscriber<T> actual;

            long remaining;

            internal SkipConditionalSubscriber(IConditionalSubscriber<T> actual, long n)
            {
                this.actual = actual;
                this.remaining = n;
            }

            public override void OnComplete()
            {
                actual.OnComplete();
            }

            public override void OnError(Exception cause)
            {
                actual.OnError(cause);
            }

            public override void OnNext(T element)
            {
                long n = remaining;
                if (n > 0)
                {
                    remaining = n - 1;
                    return;
                }
                actual.OnNext(element);
            }

            public override bool TryOnNext(T element)
            {
                long n = remaining;
                if (n > 0)
                {
                    remaining = n - 1;
                    return true;
                }
                return actual.TryOnNext(element);
            }

            public override bool Poll(out T item)
            {
                long n = remaining;
                if (n > 0)
                {
                    while (n > 0)
                    {
                        if (!qs.Poll(out T v))
                        {
                            remaining = n;
                            item = default(T);
                            return false;
                        }
                        n--;
                    }
                    remaining = 0;
                }
                return qs.Poll(out item);
            }

            protected override void OnStart(ISubscription subscription)
            {
                long n = remaining;
                actual.OnSubscribe(this);
                if (n > 0 && fusionMode != FusionSupport.SYNC)
                {
                    subscription.Request(n);
                }
            }

            public override int RequestFusion(int mode)
            {
                return base.RequestTransientFusion(mode);
            }
        }
    }
}
