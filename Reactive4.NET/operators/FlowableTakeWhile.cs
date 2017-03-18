using Reactive4.NET.subscribers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Reactive.Streams;

namespace Reactive4.NET.operators
{
    sealed class FlowableTakeWhile<T> : AbstractFlowableOperator<T, T>
    {
        readonly Func<T, bool> predicate;

        public FlowableTakeWhile(IFlowable<T> source, Func<T, bool> predicate) : base(source)
        {
            this.predicate = predicate;
        }

        public override void Subscribe(IFlowableSubscriber<T> subscriber)
        {
            source.Subscribe(new TakeWhileSubscriber(subscriber, predicate));
        }

        sealed class TakeWhileSubscriber : AbstractFuseableSubscriber<T, T>
        {
            readonly IFlowableSubscriber<T> actual;

            readonly Func<T, bool> predicate;

            internal TakeWhileSubscriber(IFlowableSubscriber<T> actual, Func<T, bool> predicate)
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
                if (qs.Poll(out T v))
                {
                    if (predicate(v))
                    {
                        item = v;
                        return true;
                    }
                    upstream.Cancel();
                    if (fusionMode == FusionSupport.ASYNC)
                    {
                        actual.OnComplete();
                    }
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
                        actual.OnNext(element);
                    } else
                    {
                        upstream.Cancel();
                        OnComplete();
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
