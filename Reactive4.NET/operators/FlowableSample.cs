using Reactive.Streams;
using Reactive4.NET.subscribers;
using Reactive4.NET.utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Reactive4.NET.operators
{
    sealed class FlowableSample<T, U> : AbstractFlowableOperator<T, T>
    {
        readonly IPublisher<U> other;

        readonly bool emitLast;

        public FlowableSample(IFlowable<T> source, IPublisher<U> other, bool emitLast) : base(source)
        {
            this.other = other;
            this.emitLast = emitLast;
        }

        public override void Subscribe(IFlowableSubscriber<T> subscriber)
        {
            var s = new HalfSerializedSubscriber<T>(subscriber);
            var parent = new SampleSubscriber(s, emitLast);
            s.OnSubscribe(parent);
            other.Subscribe(parent.other);
            source.Subscribe(parent);
        }

        sealed class SampleSubscriber : IFlowableSubscriber<T>, ISubscription
        {
            readonly IFlowableSubscriber<T> actual;

            internal readonly OtherSubscriber other;

            readonly bool emitLast;

            ISubscription upstream;

            Entry latest;

            static readonly Entry Terminated = new Entry(default(T));

            internal SampleSubscriber(IFlowableSubscriber<T> actual, bool emitLast)
            {
                this.actual = actual;
                this.emitLast = emitLast;
                this.other = new OtherSubscriber(this);
            }

            public void Cancel()
            {
                SubscriptionHelper.Cancel(ref upstream);
                other.CancelOther();
            }

            public void OnComplete()
            {
                var curr = Interlocked.Exchange(ref latest, Terminated);
                other.CancelOther();

                if (curr != Terminated)
                {
                    if (curr != null && emitLast)
                    {
                        other.CompleteLast(curr.item);
                    }
                    else
                    {
                        actual.OnComplete();
                    }
                }
            }

            public void OnError(Exception cause)
            {
                Interlocked.Exchange(ref latest, Terminated);
                other.CancelOther();
                actual.OnError(cause);
            }

            public void OnNext(T element)
            {
                var curr = Volatile.Read(ref latest);
                if (curr != Terminated)
                {
                    Interlocked.CompareExchange(ref latest, new Entry(element), curr);
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
                other.RequestOther(n);
            }

            bool OtherNext()
            {
                var curr = Volatile.Read(ref latest);
                if (curr != Terminated && curr != null)
                {
                    if (Interlocked.CompareExchange(ref latest, null, curr) == curr)
                    {
                        actual.OnNext(curr.item);
                        return true;
                    }
                }
                return false;
            }

            void OtherError(Exception ex)
            {
                Interlocked.Exchange(ref latest, Terminated);
                SubscriptionHelper.Cancel(ref upstream);
                actual.OnError(ex);
            }

            void OtherComplete()
            {
                var curr = Interlocked.Exchange(ref latest, Terminated);
                SubscriptionHelper.Cancel(ref upstream);

                if (curr != Terminated)
                {
                    if (curr != null && emitLast)
                    {
                        other.CompleteLast(curr.item);
                    }
                    else
                    {
                        actual.OnComplete();
                    }
                }
            }

            internal class Entry
            {
                internal readonly T item;

                internal Entry(T item)
                {
                    this.item = item;
                }
            }

            internal sealed class OtherSubscriber : IFlowableSubscriber<U>
            {
                readonly SampleSubscriber parent;

                ISubscription upstream;

                bool cancelled;

                long requested;

                long deferred;

                long produced;

                T last;

                internal OtherSubscriber(SampleSubscriber parent)
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
                    if (parent.OtherNext())
                    {
                        produced++;
                    }
                }

                public void OnSubscribe(ISubscription subscription)
                {
                    SubscriptionHelper.DeferredSetOnce(ref upstream, ref deferred, subscription);
                }

                internal void CancelOther()
                {
                    Volatile.Write(ref cancelled, true);
                    SubscriptionHelper.Cancel(ref upstream);
                }

                internal void RequestOther(long n)
                {
                    if (!SubscriptionHelper.PostCompleteSingleRequest(parent.actual, ref requested, ref last, n, ref cancelled))
                    {
                        SubscriptionHelper.DeferredRequest(ref upstream, ref deferred, n);
                    }
                }

                internal void CompleteLast(T entry)
                {
                    long p = produced;
                    if (p != 0L)
                    {
                        SubscriptionHelper.Produced(ref requested, p);
                    } 
                    SubscriptionHelper.PostCompleteSingleResult(parent.actual, ref requested, ref last, entry, ref cancelled);
                }
            }
        }
    }
}
