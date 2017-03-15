using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Reactive.Streams;
using Reactive4.NET.subscribers;

namespace Reactive4.NET.operators
{
    sealed class FlowableDistinctUntilChanged<T> : AbstractFlowableOperator<T, T>
    {
        readonly IEqualityComparer<T> comparer;

        public FlowableDistinctUntilChanged(IFlowable<T> source, IEqualityComparer<T> comparer) : base(source)
        {
            this.comparer = comparer;
        }

        public override void Subscribe(IFlowableSubscriber<T> subscriber)
        {
            if (subscriber is IConditionalSubscriber<T> s)
            {
                source.Subscribe(new DistinctUntilChangedConditionalSubscriber(s, comparer));
            }
            else
            {
                source.Subscribe(new DistinctUntilChangedSubscriber(subscriber, comparer));
            }
        }

        sealed class DistinctUntilChangedSubscriber : AbstractFuseableConditionalSubscriber<T, T>
        {
            readonly IFlowableSubscriber<T> actual;

            readonly IEqualityComparer<T> comparer;

            T latest;

            internal DistinctUntilChangedSubscriber(IFlowableSubscriber<T> actual, IEqualityComparer<T> comparer)
            {
                this.actual = actual;
                this.comparer = comparer;
            }

            public override void OnComplete()
            {
                if (done)
                {
                    return;
                }
                latest = default(T);
                done = true;
                actual.OnComplete();
            }

            public override void OnError(Exception cause)
            {
                if (done)
                {
                    return;
                }
                latest = default(T);
                done = true;
                actual.OnError(cause);
            }

            public override bool Poll(out T item)
            {
                for (;;)
                {
                    if (qs.Poll(out T v))
                    {
                        if (!comparer.Equals(latest, v))
                        {
                            latest = v;
                            item = v;
                            return true;
                        }
                        latest = v;
                        if (fusionMode == FusionSupport.ASYNC)
                        {
                            upstream.Request(1);
                        }
                    }
                    else
                    {
                        item = default(T);
                        return false;
                    }
                }
            }

            public override bool TryOnNext(T item)
            {
                if (done)
                {
                    return false;
                }
                if (fusionMode == FusionSupport.NONE)
                {
                    bool b;

                    try
                    {
                        b = comparer.Equals(latest, item);
                    }
                    catch (Exception ex)
                    {
                        upstream.Cancel();
                        OnError(ex);
                        return false;
                    }

                    if (!b)
                    {
                        latest = item;
                        actual.OnNext(item);
                        return true;
                    }
                    latest = item;
                    return false;
                }
                actual.OnNext(default(T));
                return true;
            }

            protected override void OnStart(ISubscription subscription)
            {
                actual.OnSubscribe(this);
            }
        }

        sealed class DistinctUntilChangedConditionalSubscriber : AbstractFuseableConditionalSubscriber<T, T>
        {
            readonly IConditionalSubscriber<T> actual;

            readonly IEqualityComparer<T> comparer;

            T latest;

            internal DistinctUntilChangedConditionalSubscriber(IConditionalSubscriber<T> actual, IEqualityComparer<T> comparer)
            {
                this.actual = actual;
                this.comparer = comparer;
            }

            public override void OnComplete()
            {
                if (done)
                {
                    return;
                }
                latest = default(T);
                done = true;
                actual.OnComplete();
            }

            public override void OnError(Exception cause)
            {
                if (done)
                {
                    return;
                }
                latest = default(T);
                done = true;
                actual.OnError(cause);
            }

            public override bool Poll(out T item)
            {
                for (;;)
                {
                    if (qs.Poll(out T v))
                    {
                        if (!comparer.Equals(latest, v))
                        {
                            latest = v;
                            item = v;
                            return true;
                        }
                        latest = v;
                        if (fusionMode == FusionSupport.ASYNC)
                        {
                            upstream.Request(1);
                        }
                    }
                    else
                    {
                        item = default(T);
                        return false;
                    }
                }
            }

            public override bool TryOnNext(T item)
            {
                if (done)
                {
                    return false;
                }
                if (fusionMode == FusionSupport.NONE)
                {
                    bool b;

                    try
                    {
                        b = comparer.Equals(latest, item);
                    }
                    catch (Exception ex)
                    {
                        upstream.Cancel();
                        OnError(ex);
                        return false;
                    }

                    if (!b)
                    {
                        latest = item;
                        return actual.TryOnNext(item);
                    }
                    latest = item;
                    return false;
                }
                actual.OnNext(default(T));
                return true;
            }

            protected override void OnStart(ISubscription subscription)
            {
                actual.OnSubscribe(this);
            }
        }
    }
}
