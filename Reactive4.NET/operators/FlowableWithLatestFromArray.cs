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
    sealed class FlowableWithLatestFromArray<T, R> : AbstractFlowableOperator<T, R>
    {
        readonly IPublisher<T>[] others;

        readonly Func<T[], R> combiner;

        public FlowableWithLatestFromArray(IFlowable<T> source, IPublisher<T>[] others, Func<T[], R> combiner) : base(source)
        {
            this.others = others;
            this.combiner = combiner;
        }

        public override void Subscribe(IFlowableSubscriber<R> subscriber)
        {
            var os = others;
            var n = os.Length;
            var parent = new WithLatestFromArraySubscriber(subscriber, combiner, n);
            subscriber.OnSubscribe(parent);
            for (int i = 0; i < n; i++)
            {
                os[i].Subscribe(parent.others[i]);
            }
            source.Subscribe(parent);
        }

        internal sealed class WithLatestFromArraySubscriber : IConditionalSubscriber<T>, ISubscription
        {
            readonly IFlowableSubscriber<R> actual;

            internal readonly OtherSubscriber[] others;

            readonly Func<T[], R> combiner;

            readonly Node[] latest;

            ISubscription upstream;
            long requested;

            bool done;

            int wip;
            Exception error;

            internal WithLatestFromArraySubscriber(IFlowableSubscriber<R> actual, Func<T[], R> combiner, int n)
            {
                this.actual = actual;
                this.combiner = combiner;
                var os = new OtherSubscriber[n];
                this.latest = new Node[n];
                for (int i = 0; i < n; i++)
                {
                    os[i] = new OtherSubscriber(this, i);
                }
                this.others = os;
            }

            public void Cancel()
            {
                SubscriptionHelper.Cancel(ref upstream);
                foreach (var inner in others)
                {
                    SubscriptionHelper.Cancel(ref inner.upstream);
                }
                var ls = latest;
                for (int i = 0; i < ls.Length; i++)
                {
                    ls[i] = null;
                }
            }

            public void OnComplete()
            {
                if (done)
                {
                    return;
                }
                done = true;
                foreach (var inner in others)
                {
                    SubscriptionHelper.Cancel(ref inner.upstream);
                }
                for (int i = 0; i < latest.Length; i++)
                {
                    latest[i] = null;
                }
                SerializationHelper.OnComplete(actual, ref wip, ref error);
            }

            public void OnError(Exception cause)
            {
                if (done)
                {
                    return;
                }
                done = true;
                foreach (var inner in others)
                {
                    SubscriptionHelper.Cancel(ref inner.upstream);
                }
                for (int i = 0; i < latest.Length; i++)
                {
                    latest[i] = null;
                }
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

                var os = others;
                var n = os.Length;
                var ls = latest;
                T[] values = new T[n + 1];
                values[0] = element;

                for (int i = 0; i < n; i++)
                {
                    var li = Volatile.Read(ref ls[i]);
                    if (li == null)
                    {
                        return false;
                    }
                    values[i + 1] = li.item;
                }

                R v;

                try
                {
                    v = combiner(values);
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

            public void OnSubscribe(ISubscription subscription)
            {
                SubscriptionHelper.DeferredSetOnce(ref upstream, ref requested, subscription);
            }

            public void Request(long n)
            {
                SubscriptionHelper.DeferredRequest(ref upstream, ref requested, n);
            }

            void OtherNext(T item, int index)
            {
                var n = new Node(item);
                Interlocked.Exchange(ref latest[index], n);
            }

            void OtherError(Exception ex, int index)
            {
                SubscriptionHelper.Cancel(ref upstream);
                var os = others;
                var n = os.Length;
                for (int i = 0; i < n; i++)
                {
                    if (i != index)
                    {
                        SubscriptionHelper.Cancel(ref os[i].upstream);
                    }
                }
                SerializationHelper.OnError(actual, ref wip, ref error, ex);
            }

            void OtherComplete(int index)
            {
                if (Volatile.Read(ref latest[index]) == null)
                {
                    SubscriptionHelper.Cancel(ref upstream);
                    var os = others;
                    var n = os.Length;
                    for (int i = 0; i < n; i++)
                    {
                        if (i != index)
                        {
                            SubscriptionHelper.Cancel(ref os[i].upstream);
                        }
                    }
                    SerializationHelper.OnComplete(actual, ref wip, ref error);
                }
            }

            sealed class Node
            {
                internal readonly T item;
                internal Node(T item)
                {
                    this.item = item;
                }
            }

            internal sealed class OtherSubscriber : IFlowableSubscriber<T>
            {
                readonly WithLatestFromArraySubscriber parent;

                internal ISubscription upstream;

                readonly int index;

                internal OtherSubscriber(WithLatestFromArraySubscriber parent, int index)
                {
                    this.parent = parent;
                    this.index = index;
                }

                public void OnComplete()
                {
                    parent.OtherComplete(index);
                }

                public void OnError(Exception cause)
                {
                    parent.OtherError(cause, index);
                }

                public void OnNext(T element)
                {
                    parent.OtherNext(element, index);
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
