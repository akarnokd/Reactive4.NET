using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Reactive.Streams;
using Reactive4.NET.subscribers;

namespace Reactive4.NET.operators
{
    sealed class FlowableMap<T, R> : AbstractFlowableOperator<T, R>
    {
        readonly Func<T, R> mapper;

        public FlowableMap(IFlowable<T> source, Func<T, R> mapper) : base(source)
        {
            this.mapper = mapper;
        }

        public override void Subscribe(IFlowableSubscriber<R> subscriber)
        {
            if (subscriber is IConditionalSubscriber<R> s)
            {
                source.Subscribe(new MapConditionalSubscriber(s, mapper));
            }
            else
            {
                source.Subscribe(new MapSubscriber(subscriber, mapper));
            }
        }

        sealed class MapSubscriber : AbstractFuseableSubscriber<T, R>
        {
            readonly IFlowableSubscriber<R> actual;

            readonly Func<T, R> mapper;

            internal MapSubscriber(IFlowableSubscriber<R> actual, Func<T, R> mapper)
            {
                this.actual = actual;
                this.mapper = mapper;
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
                if (fusionMode == FusionSupport.NONE)
                {
                    R v;
                    try
                    {
                        v = mapper(element);
                        if (v == null)
                        {
                            throw new NullReferenceException("The mapper returned a null value");
                        }
                    }
                    catch (Exception ex)
                    {
                        upstream.Cancel();
                        OnError(ex);
                        return;
                    }

                    actual.OnNext(v);
                }
                else
                {
                    actual.OnNext(default(R));
                }
            }

            public override bool Poll(out R item)
            {
                if (qs.Poll(out T v))
                {
                    R r = mapper(v);
                    if (r == null)
                    {
                        throw new NullReferenceException();
                    }
                    item = r;
                    return true;
                }
                item = default(R);
                return false;
            }

            protected override void OnStart(ISubscription subscription)
            {
                actual.OnSubscribe(this);
            }
        }

        sealed class MapConditionalSubscriber : AbstractFuseableConditionalSubscriber<T, R>
        {
            readonly IConditionalSubscriber<R> actual;

            readonly Func<T, R> mapper;

            internal MapConditionalSubscriber(IConditionalSubscriber<R> actual, Func<T, R> mapper)
            {
                this.actual = actual;
                this.mapper = mapper;
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

            public override bool TryOnNext(T element)
            {
                if (fusionMode == FusionSupport.NONE)
                {
                    R v;
                    try
                    {
                        v = mapper(element);
                        if (v == null)
                        {
                            throw new NullReferenceException("The mapper returned a null value");
                        }
                    }
                    catch (Exception ex)
                    {
                        upstream.Cancel();
                        OnError(ex);
                        return false;
                    }

                    return actual.TryOnNext(v);
                }
                return actual.TryOnNext(default(R));
            }

            public override bool Poll(out R item)
            {
                if (qs.Poll(out T v))
                {
                    R r = mapper(v);
                    if (r == null)
                    {
                        throw new NullReferenceException();
                    }
                    item = r;
                    return true;
                }
                item = default(R);
                return false;
            }

            protected override void OnStart(ISubscription subscription)
            {
                actual.OnSubscribe(this);
            }
        }
    }
}
