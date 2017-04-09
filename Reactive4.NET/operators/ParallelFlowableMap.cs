using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Reactive.Streams;

namespace Reactive4.NET.operators
{
    sealed class ParallelFlowableMap<T, R> : AbstractParallelOperator<T, R>
    {
        readonly Func<T, R> mapper;

        public ParallelFlowableMap(IParallelFlowable<T> source, Func<T, R> mapper) : base(source)
        {
            this.mapper = mapper;
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
                        parents[i] = new MapConditionalSubscriber(cs, mapper);
                    }
                    else
                    {
                        parents[i] = new MapSubscriber(s, mapper);
                    }
                }

                source.Subscribe(parents);
            }
        }

        sealed class MapSubscriber : IFlowableSubscriber<T>, ISubscription
        {
            readonly IFlowableSubscriber<R> actual;

            readonly Func<T, R> mapper;

            bool done;

            ISubscription upstream;

            internal MapSubscriber(IFlowableSubscriber<R> actual, Func<T, R> mapper)
            {
                this.actual = actual;
                this.mapper = mapper;
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
                if (done)
                {
                    return;
                }

                R v;

                try
                {
                    v = mapper(element);
                }
                catch (Exception ex)
                {
                    upstream.Cancel();
                    OnError(ex);
                    return;
                }

                actual.OnNext(v);
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

        sealed class MapConditionalSubscriber : IConditionalSubscriber<T>, ISubscription
        {
            readonly IConditionalSubscriber<R> actual;

            readonly Func<T, R> mapper;

            bool done;

            ISubscription upstream;

            internal MapConditionalSubscriber(IConditionalSubscriber<R> actual, Func<T, R> mapper)
            {
                this.actual = actual;
                this.mapper = mapper;
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

            public bool TryOnNext(T item)
            {
                if (done)
                {
                    return false;
                }

                R v;

                try
                {
                    v = mapper(item);
                }
                catch (Exception ex)
                {
                    upstream.Cancel();
                    OnError(ex);
                    return false;
                }

                return actual.TryOnNext(v);
            }
        }
    }
}
