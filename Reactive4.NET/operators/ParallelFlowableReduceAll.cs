using Reactive4.NET.utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Reactive.Streams;
using System.Threading;

namespace Reactive4.NET.operators
{
    sealed class ParallelFlowableReduceAll<T> : AbstractFlowableSource<T>
    {
        readonly IParallelFlowable<T> source;

        readonly Func<T, T, T> reducer;

        internal ParallelFlowableReduceAll(IParallelFlowable<T> source, Func<T, T, T> reducer)
        {
            this.source = source;
            this.reducer = reducer;
        }

        public override void Subscribe(IFlowableSubscriber<T> subscriber)
        {
            var src = source;
            int n = src.Parallelism;

            var parent = new ReduceAllSubscription(subscriber, n, reducer);
            subscriber.OnSubscribe(parent);
            src.Subscribe(parent.subscribers);
        }

        sealed class ReduceAllSubscription : AbstractDeferredScalarSubscription<T>
        {
            readonly Func<T, T, T> reducer;

            internal readonly ReduceRailSubscriber[] subscribers;

            int remaining;

            ValuePair pair;

            public ReduceAllSubscription(IFlowableSubscriber<T> actual, int n, Func<T, T, T> reducer) : base(actual)
            {
                var subs = new ReduceRailSubscriber[n];
                for (int i = 0; i < n; i++)
                {
                    subs[i] = new ReduceRailSubscriber(this, reducer);
                }
                this.remaining = n;
                this.reducer = reducer;
                this.subscribers = subs;
            }

            public override void Cancel()
            {
                base.Cancel();
                CancelAll();
            }

            ValuePair AddValue(T value)
            {
                for (;;)
                {
                    var p = Volatile.Read(ref pair);
                    if (p == null)
                    {
                        p = new ValuePair();
                        if (Interlocked.CompareExchange(ref pair, p, null) != null)
                        {
                            continue;
                        }
                    }

                    if (!p.Add(value))
                    {
                        Interlocked.CompareExchange(ref pair, null, p);
                        continue;
                    }

                    if (p.Finish())
                    {
                        Interlocked.CompareExchange(ref pair, null, p);
                        return p;
                    }
                    return null;
                }
            }

            void Reduce(bool hasValue, T value)
            {
                if (hasValue)
                {
                    for (;;)
                    {
                        var p = AddValue(value);
                        if (p == null)
                        {
                            break;
                        }
                        else
                        {
                            try
                            {
                                value = reducer(p.first, p.second);
                            }
                            catch (Exception ex)
                            {
                                RailError(ex);
                                return;
                            }
                        }
                    }
                }
                if (Interlocked.Decrement(ref remaining) == 0)
                {
                    var p = Volatile.Read(ref pair);
                    Volatile.Write(ref pair, null);
                    if (p == null)
                    {
                        Complete();
                    }
                    else
                    {
                        Complete(p.first);
                    }
                }
            }

            void CancelAll()
            {
                foreach (var inner in subscribers)
                {
                    inner.Cancel();
                }
            }

            void RailError(Exception cause)
            {
                if (Interlocked.Exchange(ref remaining, 0) > 0)
                {
                    CancelAll();
                    Error(cause);
                }
            }

            internal sealed class ValuePair
            {
                int index;

                int ready;

                internal T first;

                internal T second;

                internal bool Add(T item)
                {
                    var idx = Interlocked.Increment(ref index);
                    if (idx == 1)
                    {
                        first = item;
                        return true;
                    }
                    else
                    if (idx == 2)
                    {
                        second = item;
                        return true;
                    }
                    return false;
                }

                internal bool Finish()
                {
                    return Interlocked.Increment(ref ready) == 2;
                }
            }

            internal sealed class ReduceRailSubscriber : IFlowableSubscriber<T>
            {
                readonly ReduceAllSubscription parent;

                readonly Func<T, T, T> reducer;

                ISubscription upstream;

                bool hasValue;
                T value;

                internal ReduceRailSubscriber(ReduceAllSubscription parent, Func<T, T, T> reducer)
                {
                    this.parent = parent;
                    this.reducer = reducer;
                }

                public void OnComplete()
                {
                    parent.Reduce(hasValue, value);
                }

                public void OnError(Exception cause)
                {
                    value = default(T);
                    parent.RailError(cause);
                }

                public void OnNext(T element)
                {
                    if (!hasValue)
                    {
                        hasValue = true;
                        value = element;
                    }
                    else
                    {
                        try
                        {
                            value = reducer(value, element);
                        }
                        catch (Exception ex)
                        {
                            upstream.Cancel();
                            OnError(ex);
                        }
                    }
                }

                public void OnSubscribe(ISubscription subscription)
                {
                    if (SubscriptionHelper.SetOnce(ref upstream, subscription))
                    {
                        subscription.Request(long.MaxValue);
                    }
                }

                internal void Cancel()
                {
                    SubscriptionHelper.Cancel(ref upstream);
                }
            }
        }
    }
}
