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
    sealed class FlowableWithLatestFrom<T, U, R> : AbstractFlowableOperator<T, R>
    {
        readonly IPublisher<U> other;

        readonly Func<T, U, R> combiner;

        public FlowableWithLatestFrom(IFlowable<T> source, IPublisher<U> other, Func<T, U, R> combiner) : base(source)
        {
            this.other = other;
            this.combiner = combiner;
        }

        public override void Subscribe(IFlowableSubscriber<R> subscriber)
        {
            var parent = new WithLatestFromSubscriber(subscriber, combiner);
            subscriber.OnSubscribe(parent);
            other.Subscribe(parent.otherSubscriber);
            source.Subscribe(parent);
        }

        sealed class WithLatestFromSubscriber : IConditionalSubscriber<T>, ISubscription
        {
            readonly IFlowableSubscriber<R> actual;

            internal readonly OtherSubscriber otherSubscriber;

            readonly Func<T, U, R> combiner;
            
            ISubscription upstream;
            long requested;

            bool done;

            Node latest;

            int wip;
            Exception error;

            internal WithLatestFromSubscriber(IFlowableSubscriber<R> actual, Func<T, U, R> combiner)
            {
                this.actual = actual;
                this.combiner = combiner;
                this.otherSubscriber = new OtherSubscriber(this);
            }

            public void Cancel()
            {
                SubscriptionHelper.Cancel(ref upstream);
                SubscriptionHelper.Cancel(ref otherSubscriber.upstream);
            }

            public void OnComplete()
            {
                if (done)
                {
                    return;
                }
                done = true;
                SubscriptionHelper.Cancel(ref otherSubscriber.upstream);
                latest = null;
                SerializationHelper.OnComplete(actual, ref wip, ref error);
            }

            public void OnError(Exception cause)
            {
                if (done)
                {
                    return;
                }
                done = true;
                SubscriptionHelper.Cancel(ref otherSubscriber.upstream);
                latest = null;
                SerializationHelper.OnError(actual, ref wip, ref error, cause);
            }

            public void OnNext(T element)
            {
                if (!TryOnNext(element) && !done)
                {
                    upstream.Request(1);
                }
            }

            public bool TryOnNext(T element)
            {
                if (done)
                {
                    return false;
                }
                var n = Volatile.Read(ref latest);
                if (n != null)
                {
                    R v;

                    try
                    {
                        v = combiner(element, n.item);
                    }
                    catch (Exception ex)
                    {
                        Cancel();
                        OnError(ex);
                        return false;
                    }

                    SerializationHelper.OnNext(actual, ref wip, ref error, v);
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

            void OtherNext(U item)
            {
                var n = new Node(item);
                Interlocked.Exchange(ref latest, n);
            }

            void OtherError(Exception ex)
            {
                SubscriptionHelper.Cancel(ref upstream);
                SerializationHelper.OnError(actual, ref wip, ref error, ex);
            }

            void OtherComplete()
            {
                if (Volatile.Read(ref latest) == null)
                {
                    SubscriptionHelper.Cancel(ref upstream);
                    SerializationHelper.OnComplete(actual, ref wip, ref error);
                }
            }

            sealed class Node
            {
                internal readonly U item;
                internal Node(U item)
                {
                    this.item = item;
                }
            }

            internal sealed class OtherSubscriber : IFlowableSubscriber<U>
            {
                readonly WithLatestFromSubscriber parent;

                internal ISubscription upstream;

                internal OtherSubscriber(WithLatestFromSubscriber parent)
                {
                    this.parent = parent;
                }

                public void OnComplete()
                {
                    parent.OtherComplete();
                }

                public void OnError(Exception cause)
                {
                    parent.OtherError(cause);
                }

                public void OnNext(U element)
                {
                    parent.OtherNext(element);
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
}
