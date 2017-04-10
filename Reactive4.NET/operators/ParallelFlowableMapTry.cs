using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Reactive.Streams;

namespace Reactive4.NET.operators
{
    sealed class ParallelFlowableMapTry<T, R> : AbstractParallelOperator<T, R>
    {
        readonly Func<T, R> mapper;

        readonly Func<long, Exception, ParallelFailureMode> handler;

        public ParallelFlowableMapTry(IParallelFlowable<T> source, Func<T, R> mapper, Func<long, Exception, ParallelFailureMode> handler) : base(source)
        {
            this.mapper = mapper;
            this.handler = handler;
        }

        public override void Subscribe(IFlowableSubscriber<R>[] subscribers)
        {
            if (Validate(subscribers))
            {
                int n = subscribers.Length;
                var parents = new IFlowableSubscriber<T>[n];

                for (int i = 0; i < n; i++)
                {
                    var s = subscribers[i];
                    if (s is IConditionalSubscriber<R> cs)
                    {
                        parents[i] = new MapTryConditionalSubscriber(cs, mapper, handler);
                    }
                    else
                    {
                        parents[i] = new MapTrySubscriber(s, mapper, handler);
                    }
                }

                source.Subscribe(parents);
            }
        }

        sealed class MapTrySubscriber : IConditionalSubscriber<T>, ISubscription
        {
            readonly IFlowableSubscriber<R> actual;

            readonly Func<T, R> mapper;

            readonly Func<long, Exception, ParallelFailureMode> handler;

            bool done;

            ISubscription upstream;

            internal MapTrySubscriber(IFlowableSubscriber<R> actual, Func<T, R> mapper, Func<long, Exception, ParallelFailureMode> handler)
            {
                this.actual = actual;
                this.mapper = mapper;
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
                    R v;

                    try
                    {
                        v = mapper(element);
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

                    actual.OnNext(v);
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

        sealed class MapTryConditionalSubscriber : IConditionalSubscriber<T>, ISubscription
        {
            readonly IConditionalSubscriber<R> actual;

            readonly Func<T, R> mapper;

            readonly Func<long, Exception, ParallelFailureMode> handler;

            bool done;

            ISubscription upstream;

            internal MapTryConditionalSubscriber(IConditionalSubscriber<R> actual, Func<T, R> mapper, Func<long, Exception, ParallelFailureMode> handler)
            {
                this.actual = actual;
                this.mapper = mapper;
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
                    R v;

                    try
                    {
                        v = mapper(element);
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

                    return actual.TryOnNext(v);
                }
            }
        }
    }
}
