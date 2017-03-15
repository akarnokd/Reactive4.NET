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
    sealed class FlowableSkipUntil<T, U> : AbstractFlowableOperator<T, T>
    {
        readonly IPublisher<U> other;

        public FlowableSkipUntil(IFlowable<T> source, IPublisher<U> other) : base(source)
        {
            this.other = other;
        }

        public override void Subscribe(IFlowableSubscriber<T> subscriber)
        {
            if (subscriber is IConditionalSubscriber<T> s)
            {
                var parent = new SkipUntilConditionalSubscriber(s);
                subscriber.OnSubscribe(parent);

                other.Subscribe(parent.other);
                source.Subscribe(parent);
            }
            else
            {
                var parent = new SkipUntilSubscriber(subscriber);
                subscriber.OnSubscribe(parent);

                other.Subscribe(parent.other);
                source.Subscribe(parent);
            }
        }

        internal interface ISkipUntilSupport
        {
            void OtherSignal();

            void OtherError(Exception ex);
        }

        sealed class SkipUntilSubscriber : IConditionalSubscriber<T>, ISubscription, ISkipUntilSupport
        {
            readonly IFlowableSubscriber<T> actual;

            internal readonly OtherSubscriber other;

            ISubscription upstream;

            long requested;

            int wip;

            Exception error;

            int gate;

            internal SkipUntilSubscriber(IFlowableSubscriber<T> actual)
            {
                this.actual = actual;
                this.other = new OtherSubscriber(this);
            }

            public void Cancel()
            {
                SubscriptionHelper.Cancel(ref other.upstream);
                SubscriptionHelper.Cancel(ref upstream);
            }

            public void OnComplete()
            {
                SubscriptionHelper.Cancel(ref other.upstream);
                SerializationHelper.OnComplete(actual, ref wip, ref error);
            }

            public void OnError(Exception cause)
            {
                SubscriptionHelper.Cancel(ref other.upstream);
                SerializationHelper.OnError(actual, ref wip, ref error, cause);
            }

            public void OnNext(T element)
            {
                if (!TryOnNext(element))
                {
                    upstream.Request(1);
                }
            }

            public bool TryOnNext(T element)
            {
                if (Volatile.Read(ref gate) != 0)
                {
                    SerializationHelper.OnNext(actual, ref wip, ref error, element);
                    return true;
                }
                return false;
            }

            public void OnSubscribe(ISubscription subscription)
            {
                SubscriptionHelper.DeferredSetOnce(ref upstream, ref requested, subscription);
            }

            public void Request(long n)
            {
                SubscriptionHelper.DeferredRequest(ref upstream, ref requested, n);
            }

            public void OtherSignal()
            {
                Interlocked.Exchange(ref gate, 1);
            }

            public void OtherError(Exception ex)
            {
                SubscriptionHelper.Cancel(ref upstream);
                SerializationHelper.OnError(actual, ref wip, ref error, ex);
            }
        }

        sealed class SkipUntilConditionalSubscriber : IConditionalSubscriber<T>, ISubscription, ISkipUntilSupport
        {
            readonly IConditionalSubscriber<T> actual;

            internal readonly OtherSubscriber other;

            ISubscription upstream;

            long requested;

            int wip;

            Exception error;

            int gate;

            internal SkipUntilConditionalSubscriber(IConditionalSubscriber<T> actual)
            {
                this.actual = actual;
                this.other = new OtherSubscriber(this);
            }

            public void Cancel()
            {
                SubscriptionHelper.Cancel(ref other.upstream);
                SubscriptionHelper.Cancel(ref upstream);
            }

            public void OnComplete()
            {
                SubscriptionHelper.Cancel(ref other.upstream);
                SerializationHelper.OnComplete(actual, ref wip, ref error);
            }

            public void OnError(Exception cause)
            {
                SubscriptionHelper.Cancel(ref other.upstream);
                SerializationHelper.OnError(actual, ref wip, ref error, cause);
            }

            public void OnNext(T element)
            {
                if (!TryOnNext(element))
                {
                    upstream.Request(1);
                }
            }

            public bool TryOnNext(T element)
            {
                if (Volatile.Read(ref gate) != 0)
                {
                    return SerializationHelper.TryOnNext(actual, ref wip, ref error, element);
                }
                return false;
            }

            public void OnSubscribe(ISubscription subscription)
            {
                SubscriptionHelper.DeferredSetOnce(ref upstream, ref requested, subscription);
            }

            public void Request(long n)
            {
                SubscriptionHelper.DeferredRequest(ref upstream, ref requested, n);
            }

            public void OtherSignal()
            {
                Interlocked.Exchange(ref gate, 1);
            }

            public void OtherError(Exception ex)
            {
                SubscriptionHelper.Cancel(ref upstream);
                SerializationHelper.OnError(actual, ref wip, ref error, ex);
            }
        }

        internal sealed class OtherSubscriber : IFlowableSubscriber<U>
        {
            readonly ISkipUntilSupport parent;

            bool done;

            internal ISubscription upstream;

            internal OtherSubscriber(ISkipUntilSupport parent)
            {
                this.parent = parent;
            }

            public void OnComplete()
            {
                if (!done)
                {
                    done = true;
                    parent.OtherSignal();
                }
            }

            public void OnError(Exception cause)
            {
                if (!done)
                {
                    done = true;
                    parent.OtherError(cause);
                }
            }

            public void OnNext(U element)
            {
                if (!done)
                {
                    upstream.Cancel();
                    done = true;
                    parent.OtherSignal();
                }
            }

            public void OnSubscribe(ISubscription subscription)
            {
                if (SubscriptionHelper.SetOnce(ref upstream, subscription))
                {
                    subscription.Request(long.MaxValue);
                }
            }
        }
    }
}
