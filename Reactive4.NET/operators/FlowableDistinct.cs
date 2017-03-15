using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Reactive.Streams;
using Reactive4.NET.subscribers;

namespace Reactive4.NET.operators
{
    sealed class FlowableDistinct<T> : AbstractFlowableOperator<T, T>
    {
        readonly IEqualityComparer<T> comparer;

        public FlowableDistinct(IFlowable<T> source, IEqualityComparer<T> comparer) : base(source)
        {
            this.comparer = comparer;
        }

        public override void Subscribe(IFlowableSubscriber<T> subscriber)
        {
            if (subscriber is IConditionalSubscriber<T> s)
            {
                source.Subscribe(new DistinctConditionalSubscriber(s, comparer));
            }
            else
            {
                source.Subscribe(new DistinctSubscriber(subscriber, comparer));
            }
        }

        sealed class DistinctSubscriber : AbstractFuseableConditionalSubscriber<T, T>
        {
            readonly IFlowableSubscriber<T> actual;
            
            readonly ISet<T> set;

            internal DistinctSubscriber(IFlowableSubscriber<T> actual, IEqualityComparer<T> comparer)
            {
                this.actual = actual;
                this.set = new HashSet<T>(comparer);
            }

            public override void OnComplete()
            {
                set.Clear();
                actual.OnComplete();
            }

            public override void OnError(Exception cause)
            {
                set.Clear();
                actual.OnError(cause);
            }

            public override bool Poll(out T item)
            {
                for (;;)
                {
                    if (qs.Poll(out T v))
                    {
                        if (set.Add(v))
                        {
                            item = v;
                            return true;
                        }
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
                if (fusionMode == FusionSupport.NONE)
                {
                    if (set.Add(item))
                    {
                        actual.OnNext(item);
                        return true;
                    }
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

        sealed class DistinctConditionalSubscriber : AbstractFuseableConditionalSubscriber<T, T>
        {
            readonly IConditionalSubscriber<T> actual;

            readonly ISet<T> set;

            internal DistinctConditionalSubscriber(IConditionalSubscriber<T> actual, IEqualityComparer<T> comparer)
            {
                this.actual = actual;
                this.set = new HashSet<T>(comparer);
            }

            public override void OnComplete()
            {
                set.Clear();
                actual.OnComplete();
            }

            public override void OnError(Exception cause)
            {
                set.Clear();
                actual.OnComplete();
            }

            public override bool Poll(out T item)
            {
                for (;;)
                {
                    if (qs.Poll(out T v))
                    {
                        if (set.Add(v))
                        {
                            item = v;
                            return true;
                        }
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
                if (fusionMode == FusionSupport.NONE)
                {
                    if (set.Add(item))
                    {
                        return actual.TryOnNext(item);
                    }
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
