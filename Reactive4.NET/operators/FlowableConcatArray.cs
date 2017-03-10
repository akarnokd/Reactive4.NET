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
    sealed class FlowableConcatArray<T> : AbstractFlowableSource<T>
    {
        readonly IPublisher<T>[] sources;

        internal FlowableConcatArray(IPublisher<T>[] sources)
        {
            this.sources = sources;
        }

        public override void Subscribe(IFlowableSubscriber<T> subscriber)
        {
            if (subscriber is IConditionalSubscriber<T> s)
            {
                var parent = new ConcatArrayConditionalSubscriber(s, sources);
                subscriber.OnSubscribe(parent);
                parent.OnComplete();
            }
            else
            {
                var parent = new ConcatArraySubscriber(subscriber, sources);
                subscriber.OnSubscribe(parent);
                parent.OnComplete();
            }
        }

        sealed class ConcatArraySubscriber : SubscriptionArbiter, IFlowableSubscriber<T>
        {
            readonly IFlowableSubscriber<T> actual;

            readonly IPublisher<T>[] sources;

            int index;

            int wip;

            long consumed;

            internal ConcatArraySubscriber(IFlowableSubscriber<T> actual, IPublisher<T>[] sources)
            {
                this.actual = actual;
                this.sources = sources;
            }

            public void OnComplete()
            {
                if (Interlocked.Increment(ref wip) == 1)
                {
                    var a = sources;
                    do
                    {
                        if (ArbiterIsCancelled())
                        {
                            break;
                        }
                        int idx = index;
                        if (idx == a.Length)
                        {
                            actual.OnComplete();
                            return;
                        }

                        var p = a[idx];
                        if (p == null)
                        {
                            actual.OnError(new NullReferenceException("One of the IPublishers was null"));
                            return;
                        }
                        index = idx + 1;
                        long c = consumed;
                        if (c != 0L)
                        {
                            consumed = 0L;
                            ArbiterProduced(c);
                        }
                        p.Subscribe(this);
                    }
                    while (Interlocked.Decrement(ref wip) != 0);
                }
            }

            public void OnError(Exception cause)
            {
                actual.OnError(cause);
            }

            public void OnNext(T element)
            {
                consumed++;
                actual.OnNext(element);
            }

            public void OnSubscribe(ISubscription subscription)
            {
                ArbiterSet(subscription);
            }
        }

        sealed class ConcatArrayConditionalSubscriber : SubscriptionArbiter, IConditionalSubscriber<T>
        {
            readonly IConditionalSubscriber<T> actual;

            readonly IPublisher<T>[] sources;

            int index;

            int wip;

            long consumed;

            internal ConcatArrayConditionalSubscriber(IConditionalSubscriber<T> actual, IPublisher<T>[] sources)
            {
                this.actual = actual;
                this.sources = sources;
            }

            public void OnComplete()
            {
                if (Interlocked.Increment(ref wip) == 1)
                {
                    var a = sources;
                    do
                    {
                        if (ArbiterIsCancelled())
                        {
                            break;
                        }
                        int idx = index;
                        if (idx == a.Length)
                        {
                            actual.OnComplete();
                            return;
                        }

                        var p = a[idx];
                        if (p == null)
                        {
                            actual.OnError(new NullReferenceException("One of the IPublishers was null"));
                            return;
                        }
                        index = idx + 1;
                        long c = consumed;
                        if (c != 0L)
                        {
                            consumed = 0L;
                            ArbiterProduced(c);
                        }
                        p.Subscribe(this);
                    }
                    while (Interlocked.Decrement(ref wip) != 0);
                }
            }

            public void OnError(Exception cause)
            {
                actual.OnError(cause);
            }

            public void OnNext(T element)
            {
                consumed++;
                actual.OnNext(element);
            }

            public bool TryOnNext(T element)
            {
                if (actual.TryOnNext(element))
                {
                    consumed++;
                    return true;
                }
                return false;
            }

            public void OnSubscribe(ISubscription subscription)
            {
                ArbiterSet(subscription);
            }
        }
    }
}
