using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Reactive.Streams;

namespace Reactive4.NET.operators
{
    sealed class ParallelFlowableFilter<T> : AbstractParallelOperator<T, T>
    {
        readonly Func<T, bool> predicate;

        public ParallelFlowableFilter(IParallelFlowable<T> source, Func<T, bool> predicate) : base(source)
        {
            this.predicate = predicate;
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
                        parents[i] = new FilterConditionalSubscriber(cs, predicate);
                    }
                    else
                    {
                        parents[i] = new FilterSubscriber(s, predicate);
                    }
                }

                source.Subscribe(parents);
            }
        }

        sealed class FilterSubscriber : IConditionalSubscriber<T>, ISubscription
        {
            readonly IFlowableSubscriber<T> actual;

            readonly Func<T, bool> predicate;

            bool done;

            ISubscription upstream;

            internal FilterSubscriber(IFlowableSubscriber<T> actual, Func<T, bool> predicate)
            {
                this.actual = actual;
                this.predicate = predicate;
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

                bool v;

                try
                {
                    v = predicate(item);
                }
                catch (Exception ex)
                {
                    upstream.Cancel();
                    OnError(ex);
                    return false;
                }

                if (v)
                {
                    actual.OnNext(item);
                    return true;
                }
                return false;
            }
        }

        sealed class FilterConditionalSubscriber : IConditionalSubscriber<T>, ISubscription
        {
            readonly IConditionalSubscriber<T> actual;

            readonly Func<T, bool> predicate;

            bool done;

            ISubscription upstream;

            internal FilterConditionalSubscriber(IConditionalSubscriber<T> actual, Func<T, bool> predicate)
            {
                this.actual = actual;
                this.predicate = predicate;
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

                bool v;

                try
                {
                    v = predicate(item);
                }
                catch (Exception ex)
                {
                    upstream.Cancel();
                    OnError(ex);
                    return false;
                }

                return v && actual.TryOnNext(item);
            }
        }
    }
}
