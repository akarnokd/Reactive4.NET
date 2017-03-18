using Reactive4.NET.subscribers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Reactive.Streams;

namespace Reactive4.NET.operators
{
    sealed class FlowableTakeUntilPredicate<T> : AbstractFlowableOperator<T, T>
    {
        readonly Func<T, bool> predicate;

        public FlowableTakeUntilPredicate(IFlowable<T> source, Func<T, bool> predicate) : base(source)
        {
            this.predicate = predicate;
        }

        public override void Subscribe(IFlowableSubscriber<T> subscriber)
        {
            source.Subscribe(new TakeUntilSubscriber(subscriber, predicate));
        }

        sealed class TakeUntilSubscriber : AbstractFuseableSubscriber<T, T>
        {
            readonly IFlowableSubscriber<T> actual;

            readonly Func<T, bool> predicate;

            internal TakeUntilSubscriber(IFlowableSubscriber<T> actual, Func<T, bool> predicate)
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

            public override bool Poll(out T item)
            {
                if (!done && qs.Poll(out T v))
                {
                    item = v;
                    if (predicate(v))
                    {
                        done = true;
                        upstream.Cancel();
                        if (fusionMode == FusionSupport.ASYNC)
                        {
                            actual.OnComplete();
                        }
                    }
                    return true;
                }
                item = default(T);
                return false;
            }

            public override void OnNext(T element)
            {
                if (done)
                {
                    return;
                }
                if (fusionMode == FusionSupport.NONE)
                {
                    actual.OnNext(element);

                    bool b;

                    try
                    {
                        b = predicate(element);
                    }
                    catch (Exception ex)
                    {
                        upstream.Cancel();
                        OnError(ex);
                        return;
                    }
                    if (b)
                    {
                        upstream.Cancel();
                        OnComplete();
                        return;
                    }
                }
                else
                {
                    actual.OnNext(default(T));
                }
            }

            protected override void OnStart(ISubscription subscription)
            {
                actual.OnSubscribe(this);
            }
        }
    }
}
