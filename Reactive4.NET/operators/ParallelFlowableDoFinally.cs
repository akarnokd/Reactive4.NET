using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Reactive.Streams;
using System.Threading;

namespace Reactive4.NET.operators
{
    sealed class ParallelFlowableDoFinally<T> : AbstractParallelOperator<T, T>
    {
        readonly Action onFinally;

        public ParallelFlowableDoFinally(IParallelFlowable<T> source, Action onFinally) : base(source)
        {
            this.onFinally = onFinally;
        }

        public override void Subscribe(IFlowableSubscriber<T>[] subscribers)
        {
            if (Validate(subscribers))
            {
                int n = subscribers.Length;
                var parents = new IFlowableSubscriber<T>[n];

                for (int i = 0; i < n; i++)
                {
                    var s = subscribers[i];
                    if (s is IConditionalSubscriber<T> cs)
                    {
                        parents[i] = new DoFinallyConditionalSubscriber(cs, onFinally);
                    }
                    else
                    {
                        parents[i] = new DoFinallySubscriber(s, onFinally);
                    }
                }

                source.Subscribe(parents);
            }
        }

        sealed class DoFinallySubscriber : IFlowableSubscriber<T>, ISubscription
        {
            readonly IFlowableSubscriber<T> actual;

            readonly Action onFinally;

            ISubscription upstream;

            bool done;

            int once;

            internal DoFinallySubscriber(IFlowableSubscriber<T> actual, Action onFinally)
            {
                this.actual = actual;
                this.onFinally = onFinally;
            }

            public void Cancel()
            {
                upstream.Cancel();
                OnFinally();
            }

            public void OnComplete()
            {
                if (done)
                {
                    return;
                }
                done = true;

                actual.OnComplete();

                OnFinally();
            }

            void OnFinally()
            {
                if (Interlocked.CompareExchange(ref once, 1, 0) == 0)
                {
                    try
                    {
                        onFinally.Invoke();
                    }
                    catch
                    {
                        // TODO what should happen with these?
                    }
                }
            }

            public void OnError(Exception cause)
            {
                if (done)
                {
                    return;
                }
                done = true;

                actual.OnError(cause);

                OnFinally();
            }

            public void OnNext(T element)
            {
                actual.OnNext(element);
            }

            public void OnSubscribe(ISubscription subscription)
            {
                if (SubscriptionHelper.Validate(ref upstream, subscription))
                {
                    actual.OnSubscribe(this);
                }
            }

            public void Request(long n)
            {
                upstream.Request(n);
            }
        }

        sealed class DoFinallyConditionalSubscriber : IConditionalSubscriber<T>, ISubscription
        {
            readonly IConditionalSubscriber<T> actual;

            readonly Action onFinally;

            ISubscription upstream;

            bool done;

            int once;

            internal DoFinallyConditionalSubscriber(IConditionalSubscriber<T> actual, Action onFinally)
            {
                this.actual = actual;
                this.onFinally = onFinally;
            }

            public void Cancel()
            {
                upstream.Cancel();
                OnFinally();
            }

            public void OnComplete()
            {
                if (done)
                {
                    return;
                }
                done = true;

                actual.OnComplete();

                OnFinally();
            }

            void OnFinally()
            {
                if (Interlocked.CompareExchange(ref once, 1, 0) == 0)
                {
                    try
                    {
                        onFinally.Invoke();
                    }
                    catch
                    {
                        // TODO what should happen with these?
                    }
                }
            }

            public void OnError(Exception cause)
            {
                if (done)
                {
                    return;
                }
                done = true;

                actual.OnError(cause);

                OnFinally();
            }

            public void OnNext(T element)
            {
                actual.OnNext(element);
            }

            public void OnSubscribe(ISubscription subscription)
            {
                if (SubscriptionHelper.Validate(ref upstream, subscription))
                {
                    actual.OnSubscribe(this);
                }
            }

            public void Request(long n)
            {
                upstream.Request(n);
            }

            public bool TryOnNext(T element)
            {
                return actual.TryOnNext(element);
            }
        }
    }
}
