using Reactive4.NET.subscribers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Reactive.Streams;
using System.Threading;

namespace Reactive4.NET.operators
{
    sealed class FlowableDoFinally<T> : AbstractFlowableOperator<T, T>
    {
        readonly Action onFinally;

        public FlowableDoFinally(IFlowable<T> source, Action onFinally) : base(source)
        {
            this.onFinally = onFinally;
        }

        public override void Subscribe(IFlowableSubscriber<T> subscriber)
        {
            if (subscriber is IConditionalSubscriber<T> s)
            {
                source.Subscribe(new DoFinallyConditionalSubscriber(s, onFinally));
            }
            else
            {
                source.Subscribe(new DoFinallySubscriber(subscriber, onFinally));
            }
        }

        sealed class DoFinallySubscriber : AbstractFuseableSubscriber<T, T>
        {
            readonly IFlowableSubscriber<T> actual;

            readonly Action onFinally;

            int once;

            internal DoFinallySubscriber(IFlowableSubscriber<T> actual, Action onFinally)
            {
                this.actual = actual;
                this.onFinally = onFinally;
            }

            void Finally()
            {
                if (Interlocked.CompareExchange(ref once, 1, 0) == 0)
                {
                    try
                    {
                        onFinally();
                    }
                    catch
                    {
                        // TODO handle?!
                    }
                }
            }

            public override void OnComplete()
            {
                actual.OnComplete();
                Finally();
            }

            public override void OnError(Exception cause)
            {
                actual.OnError(cause);
                Finally();
            }

            public override void OnNext(T element)
            {
                actual.OnNext(element);
            }

            public override bool Poll(out T item)
            {
                bool b = qs.Poll(out item);
                if (fusionMode == FusionSupport.SYNC && !b)
                {
                    Finally();
                }
                return b;
            }

            protected override void OnStart(ISubscription subscription)
            {
                actual.OnSubscribe(this);
            }

            public override void Cancel()
            {
                base.Cancel();
                Finally();
            }
        }

        sealed class DoFinallyConditionalSubscriber : AbstractFuseableConditionalSubscriber<T, T>
        {
            readonly IConditionalSubscriber<T> actual;

            readonly Action onFinally;

            int once;

            internal DoFinallyConditionalSubscriber(IConditionalSubscriber<T> actual, Action onFinally)
            {
                this.actual = actual;
                this.onFinally = onFinally;
            }

            void Finally()
            {
                if (Interlocked.CompareExchange(ref once, 1, 0) == 0)
                {
                    try
                    {
                        onFinally();
                    }
                    catch
                    {
                        // TODO handle?!
                    }
                }
            }

            public override void OnComplete()
            {
                actual.OnComplete();
                Finally();
            }

            public override void OnError(Exception cause)
            {
                actual.OnError(cause);
                Finally();
            }

            public override void OnNext(T element)
            {
                actual.OnNext(element);
            }

            public override bool TryOnNext(T element)
            {
                return actual.TryOnNext(element);
            }

            public override bool Poll(out T item)
            {
                bool b = qs.Poll(out item);
                if (fusionMode == FusionSupport.SYNC && !b)
                {
                    Finally();
                }
                return b;
            }

            protected override void OnStart(ISubscription subscription)
            {
                actual.OnSubscribe(this);
            }
            public override void Cancel()
            {
                base.Cancel();
                Finally();
            }
        }
    }
}
