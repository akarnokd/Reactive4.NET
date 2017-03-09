using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Reactive.Streams;
using Reactive4.NET.subscribers;

namespace Reactive4.NET.operators
{
    sealed class FlowableFilter<T> : AbstractFlowableOperator<T, T>
    {
        readonly Func<T, bool> predicate;

        public FlowableFilter(IFlowable<T> source, Func<T, bool> predicate) : base(source)
        {
            this.predicate = predicate;
        }

        public override void Subscribe(IFlowableSubscriber<T> subscriber)
        {
            if (subscriber is IConditionalSubscriber<T> s)
            {
                source.Subscribe(new FilterConditionalSubscriber(s, predicate));
            }
            else
            {
                source.Subscribe(new FilterSubscriber(subscriber, predicate));
            }
        }

        sealed class FilterSubscriber : AbstractFuseableConditionalSubscriber<T, T>
        {
            readonly IFlowableSubscriber<T> actual;

            readonly Func<T, bool> predicate;

            internal FilterSubscriber(IFlowableSubscriber<T> actual, Func<T, bool> predicate)
            {
                this.actual = actual;
                this.predicate = predicate;
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
                if (!TryOnNext(element))
                {
                    upstream.Request(1);
                }
            }

            public override bool Poll(out T item)
            {
                for (;;)
                {
                    bool b = qs.Poll(out T v);
                    if (b)
                    {
                        if (predicate(v))
                        {
                            item = v;
                            return true;
                        }
                        if (fusionMode != FusionSupport.SYNC)
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
                    bool b;

                    try
                    {
                        b = predicate(item);
                    }
                    catch (Exception ex)
                    {
                        upstream.Cancel();
                        OnError(ex);
                        return false;
                    }

                    if (b)
                    {
                        actual.OnNext(item);
                    }
                    return b;
                }
                actual.OnNext(default(T));
                return true;
            }

            protected override void OnStart(ISubscription subscription)
            {
                actual.OnSubscribe(this);
            }
        }

        sealed class FilterConditionalSubscriber : AbstractFuseableConditionalSubscriber<T, T>
        {
            readonly IConditionalSubscriber<T> actual;

            readonly Func<T, bool> predicate;

            internal FilterConditionalSubscriber(IConditionalSubscriber<T> actual, Func<T, bool> predicate)
            {
                this.actual = actual;
                this.predicate = predicate;
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
                if (!TryOnNext(element))
                {
                    upstream.Request(1);
                }
            }

            public override bool Poll(out T item)
            {
                for (;;)
                {
                    bool b = qs.Poll(out T v);
                    if (b)
                    {
                        if (predicate(v))
                        {
                            item = v;
                            return true;
                        }
                        if (fusionMode != FusionSupport.SYNC)
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
                    bool b;

                    try
                    {
                        b = predicate(item);
                    }
                    catch (Exception ex)
                    {
                        upstream.Cancel();
                        OnError(ex);
                        return false;
                    }

                    if (b)
                    {
                        return actual.TryOnNext(item);
                    }
                    return false;
                }
                return actual.TryOnNext(default(T));
            }

            protected override void OnStart(ISubscription subscription)
            {
                actual.OnSubscribe(this);
            }
        }
    }
}
