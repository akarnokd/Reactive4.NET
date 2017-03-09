using Reactive4.NET.subscribers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Reactive.Streams;
using System.Threading;

namespace Reactive4.NET.operators
{
    sealed class FlowableTake<T> : AbstractFlowableOperator<T, T>
    {
        readonly long n;

        readonly bool limitRequest;

        internal FlowableTake(IFlowable<T> source, long n, bool limitRequest) : base(source)
        {
            this.n = n;
            this.limitRequest = limitRequest;
        }

        public override void Subscribe(IFlowableSubscriber<T> subscriber)
        {
            if (subscriber is IConditionalSubscriber<T> s)
            {
                source.Subscribe(new TakeConditionalSubscriber(s, n, limitRequest));
            }
            else
            {
                source.Subscribe(new TakeSubscriber(subscriber, n, limitRequest));
            }
        }

        sealed class TakeSubscriber : AbstractFuseableSubscriber<T, T>
        {
            readonly IFlowableSubscriber<T> actual;

            readonly bool limitRequest;

            readonly long limit;

            long remaining;

            int once;

            internal TakeSubscriber(IFlowableSubscriber<T> actual, long n, bool limitRequest)
            {
                this.actual = actual;
                this.remaining = n;
                this.limit = n;
                this.limitRequest = limitRequest;
            }

            public override void OnComplete()
            {
                if (!done)
                {
                    done = true;
                    actual.OnComplete();
                }
            }

            public override void OnError(Exception cause)
            {
                if (!done)
                {
                    done = true;
                    actual.OnError(cause);
                }
            }

            public override void OnNext(T element)
            {
                if (!done)
                {
                    if (fusionMode == FusionSupport.NONE)
                    {
                        long remaining = this.remaining - 1;
                        if (remaining == 0)
                        {
                            upstream.Cancel();
                            done = true;
                            actual.OnNext(element);
                            actual.OnComplete();
                        }
                        else
                        {
                            this.remaining = remaining;
                            actual.OnNext(element);
                        }
                    }
                    else
                    {
                        actual.OnNext(default(T));
                    }
                }
            }

            public override bool Poll(out T item)
            {
                long remaining = this.remaining;
                if (remaining != 0L)
                {
                    if (qs.Poll(out item))
                    {
                        this.remaining = remaining - 1;
                        return true;
                    }
                    return false;
                }
                if (!done)
                {
                    if (fusionMode == FusionSupport.ASYNC)
                    {
                        upstream.Cancel();
                        OnComplete();
                    }
                    else
                    {
                        done = true;
                    }
                }
                item = default(T);
                return false;
            }

            protected override void OnStart(ISubscription subscription)
            {
                if (remaining == 0)
                {
                    subscription.Cancel();
                    actual.OnSubscribe(EmptySubscription<T>.Instance);
                    OnComplete();
                }
                else
                {
                    actual.OnSubscribe(this);
                }
            }

            public override int RequestFusion(int mode)
            {
                return base.RequestTransientFusion(mode);
            }

            public override void Request(long n)
            {
                if (SubscriptionHelper.Validate(n))
                {
                    if (Volatile.Read(ref once) == 0 && Interlocked.CompareExchange(ref once, 1, 0) == 0)
                    {
                        if (n >= limit)
                        {
                            upstream.Request(limitRequest ? limit : long.MaxValue);
                        }
                        else
                        {
                            upstream.Request(n);
                        }
                    }
                    else
                    {
                        upstream.Request(n);
                    }
                }
            }
        }

        sealed class TakeConditionalSubscriber : AbstractFuseableConditionalSubscriber<T, T>
        {
            readonly IConditionalSubscriber<T> actual;

            readonly bool limitRequest;

            readonly long limit;

            long remaining;

            int once;

            internal TakeConditionalSubscriber(IConditionalSubscriber<T> actual, long n, bool limitRequest)
            {
                this.actual = actual;
                this.remaining = n;
                this.limit = n;
                this.limitRequest = limitRequest;
            }

            public override void OnComplete()
            {
                if (!done)
                {
                    done = true;
                    actual.OnComplete();
                }
            }

            public override void OnError(Exception cause)
            {
                if (!done)
                {
                    done = true;
                    actual.OnError(cause);
                }
            }

            public override bool TryOnNext(T element)
            {
                if (!done)
                {
                    if (fusionMode == FusionSupport.NONE)
                    {
                        long remaining = this.remaining - 1;
                        if (remaining == 0)
                        {
                            upstream.Cancel();
                            done = true;
                            actual.TryOnNext(element);
                            actual.OnComplete();
                            return false;
                        }
                        this.remaining = remaining;
                        return actual.TryOnNext(element);
                    }
                    return actual.TryOnNext(default(T));
                }
                return false;
            }

            public override bool Poll(out T item)
            {
                long remaining = this.remaining;
                if (remaining != 0L)
                {
                    if (qs.Poll(out item))
                    {
                        this.remaining = remaining - 1;
                        return true;
                    }
                    return false;
                }
                if (!done)
                {
                    if (fusionMode == FusionSupport.ASYNC)
                    {
                        upstream.Cancel();
                        OnComplete();
                    }
                    else
                    {
                        done = true;
                    }
                }
                item = default(T);
                return false;
            }

            protected override void OnStart(ISubscription subscription)
            {
                if (limit == 0)
                {
                    subscription.Cancel();
                    actual.OnSubscribe(EmptySubscription<T>.Instance);
                    OnComplete();
                }
                else
                {
                    actual.OnSubscribe(this);
                }
            }

            public override int RequestFusion(int mode)
            {
                return base.RequestTransientFusion(mode);
            }

            public override void Request(long n)
            {
                if (SubscriptionHelper.Validate(n))
                {
                    if (Volatile.Read(ref once) == 0 && Interlocked.CompareExchange(ref once, 1, 0) == 0)
                    {
                        if (n >= limit)
                        {
                            upstream.Request(limitRequest ? limit : long.MaxValue);
                        }
                        else
                        {
                            upstream.Request(n);
                        }
                    }
                    else
                    {
                        upstream.Request(n);
                    }
                }
            }
        }
    }
}
