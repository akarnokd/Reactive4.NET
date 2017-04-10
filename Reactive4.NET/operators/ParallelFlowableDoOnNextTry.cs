using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Reactive.Streams;

namespace Reactive4.NET.operators
{
    sealed class ParallelFlowableDoOnNextTry<T> : AbstractParallelOperator<T, T>
    {
        readonly Action<T> onNext;

        readonly Func<long, Exception, ParallelFailureMode> handler;

        public ParallelFlowableDoOnNextTry(IParallelFlowable<T> source, Action<T> onNext, Func<long, Exception, ParallelFailureMode> handler) : base(source)
        {
            this.onNext = onNext;
            this.handler = handler;
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
                        parents[i] = new DoOnNextTryConditionalSubscriber(cs, onNext, handler);
                    }
                    else
                    {
                        parents[i] = new DoOnNextTrySubscriber(s, onNext, handler);
                    }
                }

                source.Subscribe(parents);
            }
        }

        sealed class DoOnNextTrySubscriber : IConditionalSubscriber<T>, ISubscription
        {
            readonly IFlowableSubscriber<T> actual;

            readonly Action<T> onNext;

            readonly Func<long, Exception, ParallelFailureMode> handler;

            bool done;

            ISubscription upstream;

            internal DoOnNextTrySubscriber(IFlowableSubscriber<T> actual, Action<T> onNext, Func<long, Exception, ParallelFailureMode> handler)
            {
                this.actual = actual;
                this.onNext = onNext;
                this.handler = handler;
            }

            public void Cancel()
            {
                upstream.Cancel();
            }

            public void OnComplete()
            {
                if (done)
                {
                    return;
                }
                done = true;
                actual.OnComplete();
            }

            public void OnError(Exception cause)
            {
                if (done)
                {
                    return;
                }
                done = true;
                actual.OnError(cause);
            }

            public void OnNext(T element)
            {
                if (!TryOnNext(element) && !done)
                {
                    upstream.Request(1);
                }
            }

            public bool TryOnNext(T element)
            {
                if (done)
                {
                    return false;
                }

                long retry = 0;
                for (;;)
                {
                    try
                    {
                        onNext(element);
                    }
                    catch (Exception ex)
                    {

                        switch (handler(++retry, ex))
                        {
                            case ParallelFailureMode.Error:
                                upstream.Cancel();
                                OnError(ex);
                                return false;
                            case ParallelFailureMode.Skip:
                                return false;
                            case ParallelFailureMode.Complete:
                                upstream.Cancel();
                                OnComplete();
                                return false;
                            default:
                                continue;
                        }

                    }

                    actual.OnNext(element);
                    return true;
                }
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

        sealed class DoOnNextTryConditionalSubscriber : IConditionalSubscriber<T>, ISubscription
        {
            readonly IConditionalSubscriber<T> actual;

            readonly Action<T> onNext;

            readonly Func<long, Exception, ParallelFailureMode> handler;

            bool done;

            ISubscription upstream;

            internal DoOnNextTryConditionalSubscriber(IConditionalSubscriber<T> actual, Action<T> onNext, Func<long, Exception, ParallelFailureMode> handler)
            {
                this.actual = actual;
                this.onNext = onNext;
                this.handler = handler;
            }

            public void Cancel()
            {
                upstream.Cancel();
            }

            public void OnComplete()
            {
                if (done)
                {
                    return;
                }
                done = true;
                actual.OnComplete();
            }

            public void OnError(Exception cause)
            {
                if (done)
                {
                    return;
                }
                done = true;
                actual.OnError(cause);
            }

            public void OnNext(T element)
            {
                if (!TryOnNext(element) && !done)
                {
                    upstream.Request(1);
                }
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
                if (done)
                {
                    return false;
                }

                long retry = 0;
                for (;;)
                {
                    try
                    {
                        onNext(element);
                    }
                    catch (Exception ex)
                    {

                        switch (handler(++retry, ex))
                        {
                            case ParallelFailureMode.Error:
                                upstream.Cancel();
                                OnError(ex);
                                return false;
                            case ParallelFailureMode.Skip:
                                return false;
                            case ParallelFailureMode.Complete:
                                upstream.Cancel();
                                OnComplete();
                                return false;
                            default:
                                continue;
                        }

                    }

                    return actual.TryOnNext(element);
                }
            }
        }
    }
}
