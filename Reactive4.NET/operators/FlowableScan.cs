using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Reactive.Streams;
using Reactive4.NET.subscribers;

namespace Reactive4.NET.operators
{
    sealed class FlowableScan<T> : AbstractFlowableOperator<T, T>
    {
        readonly Func<T, T, T> scanner;

        public FlowableScan(IFlowable<T> source, Func<T, T, T> scanner) : base(source)
        {
            this.scanner = scanner;
        }

        public override void Subscribe(IFlowableSubscriber<T> subscriber)
        {
            source.Subscribe(new ScanSubscriber(subscriber, scanner));
        }

        sealed class ScanSubscriber : AbstractFuseableSubscriber<T, T>
        {
            readonly IFlowableSubscriber<T> actual;

            readonly Func<T, T, T> scanner;

            T accumulator;
            bool hasValue;

            internal ScanSubscriber(IFlowableSubscriber<T> actual, Func<T, T, T> scanner)
            {
                this.actual = actual;
                this.scanner = scanner;
            }

            public override void OnComplete()
            {
                if (done)
                {
                    return;
                }
                done = true;
                actual.OnComplete();
            }

            public override void OnError(Exception cause)
            {
                if (done)
                {
                    return;
                }
                done = true;
                actual.OnError(cause);
            }

            public override void OnNext(T element)
            {
                if (done)
                {
                    return;
                }
                if (fusionMode == FusionSupport.ASYNC)
                {
                    actual.OnNext(default(T));
                    return;
                }
                if (!hasValue)
                {
                    accumulator = element;
                    hasValue = true;
                    actual.OnNext(element);
                }
                else
                {
                    T acc;

                    try
                    {
                        acc = scanner(accumulator, element);
                    }
                    catch (Exception ex)
                    {
                        upstream.Cancel();
                        OnError(ex);
                        return;
                    }

                    accumulator = acc;
                    actual.OnNext(acc);
                }
            }

            public override bool Poll(out T item)
            {
                T t;
                if (hasValue)
                {
                    if (qs.Poll(out t))
                    {
                        var acc = scanner(accumulator, t);
                        accumulator = acc;
                        item = acc;
                        return true;
                    }
                    item = default(T);
                    return false;
                }
                if (qs.Poll(out t))
                {
                    accumulator = t;
                    item = t;
                    hasValue = true;
                    return true;
                }
                item = default(T);
                return false;
            }

            protected override void OnStart(ISubscription subscription)
            {
                actual.OnSubscribe(this);
            }
        }
    }
}
