using Reactive.Streams;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Reactive4.NET.operators
{
    sealed class FlowableAmbArray<T> : AbstractFlowableSource<T>
    {
        readonly IPublisher<T>[] sources;

        internal FlowableAmbArray(IPublisher<T>[] sources)
        {
            this.sources = sources;
        }

        public override void Subscribe(IFlowableSubscriber<T> subscriber)
        {
            var s = sources;
            var parent = new AmbSubscription(subscriber, s.Length);
            subscriber.OnSubscribe(parent);
            parent.Subscribe(s);
        }

        internal sealed class AmbSubscription : ISubscription
        {
            readonly IFlowableSubscriber<T> actual;

            readonly AmbSubscriber[] subscribers;

            int winner = -1;

            bool cancelled;

            internal AmbSubscription(IFlowableSubscriber<T> actual, int n)
            {
                this.actual = actual;
                var sa = new AmbSubscriber[n];
                for (int i = 0; i < n; i++)
                {
                    sa[i] = new AmbSubscriber(this, i);
                }
                this.subscribers = sa;
            }

            internal void Subscribe(IPublisher<T>[] sources)
            {
                var s = subscribers;
                var n = s.Length;
                for (int i = 0; i < n; i++)
                {
                    if (Volatile.Read(ref winner) != -1 || Volatile.Read(ref cancelled))
                    {
                        break;
                    }
                    sources[i].Subscribe(s[i]);
                }
            }

            public void Cancel()
            {
                Volatile.Write(ref cancelled, true);
                foreach (var s in subscribers)
                {
                    s.Cancel();
                }
            }

            public void Request(long n)
            {
                if (SubscriptionHelper.Validate(n))
                {
                    int w = Volatile.Read(ref winner);
                    if (w == -1)
                    {
                        foreach (var s in subscribers)
                        {
                            s.Request(n);
                        }
                    }
                    else
                    {
                        subscribers[w].Request(n);
                    }
                }
            }

            bool TryWin(int index)
            {
                int w = Volatile.Read(ref winner);
                if (w >= 0)
                {
                    return w == index;
                }
                if (Interlocked.CompareExchange(ref winner, index, -1) == -1)
                {
                    var s = subscribers;
                    var n = s.Length;
                    for (int i = 0; i < n; i++)
                    {
                        if (i != index)
                        {
                            s[i].Cancel();
                        }
                    }
                    return true;
                }
                return false;
            }

            internal sealed class AmbSubscriber : ISubscriber<T>
            {
                readonly IFlowableSubscriber<T> actual;

                readonly AmbSubscription parent;

                readonly int index;

                ISubscription upstream;

                long requested;

                bool won;

                internal AmbSubscriber(AmbSubscription parent, int index)
                {
                    this.parent = parent;
                    this.actual = parent.actual;
                    this.index = index;
                }

                public void OnComplete()
                {
                    if (won)
                    {
                        actual.OnComplete();
                    }
                    else
                    if (parent.TryWin(index))
                    {
                        won = true;
                        actual.OnComplete();
                    }
                }

                public void OnError(Exception cause)
                {
                    if (won)
                    {
                        actual.OnError(cause);
                    }
                    else
                    if (parent.TryWin(index))
                    {
                        won = true;
                        actual.OnError(cause);
                    }
                }

                public void OnNext(T element)
                {
                    if (won)
                    {
                        actual.OnNext(element);
                    }
                    else
                    if (parent.TryWin(index))
                    {
                        won = true;
                        actual.OnNext(element);
                    }
                    else
                    {
                        Cancel();
                    }
                }

                public void OnSubscribe(ISubscription subscription)
                {
                    SubscriptionHelper.DeferredSetOnce(ref upstream, ref requested, subscription);
                }

                internal void Cancel()
                {
                    SubscriptionHelper.Cancel(ref upstream);
                }

                internal void Request(long n)
                {
                    SubscriptionHelper.DeferredRequest(ref upstream, ref requested, n);
                }
            }
        }
    }
}
