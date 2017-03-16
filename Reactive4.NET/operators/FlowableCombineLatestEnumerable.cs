using Reactive.Streams;
using Reactive4.NET.utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Reactive4.NET.operators
{
    sealed class FlowableCombineLatestEnumerable<T, R> : AbstractFlowableSource<R>
    {
        readonly IEnumerable<IPublisher<T>> sources;

        readonly Func<T[], R> combiner;

        readonly int prefetch;

        internal FlowableCombineLatestEnumerable(IEnumerable<IPublisher<T>> sources, Func<T[], R> combiner, int prefetch)
        {
            this.sources = sources;
            this.combiner = combiner;
            this.prefetch = prefetch;
        }

        public override void Subscribe(IFlowableSubscriber<R> subscriber)
        {
            var s = sources;

            var a = new IPublisher<T>[8];

            int n = 0;

            try
            {
                foreach (var p in sources)
                {
                    if (p == null)
                    {
                        throw new NullReferenceException("One of the source IPublishers is null");
                    }
                    if (n == a.Length)
                    {
                        var b = new IPublisher<T>[n + (n >> 2)];
                        Array.Copy(a, 0, b, 0, n);
                        a = b;
                    }
                    a[n++] = p;
                }
            }
            catch (Exception ex)
            {
                subscriber.OnSubscribe(EmptySubscription<T>.Instance);
                subscriber.OnError(ex);
                return;
            }

            if (n == 0)
            {
                subscriber.OnSubscribe(EmptySubscription<T>.Instance);
                subscriber.OnComplete();
                return;
            }
            if (n == 1)
            {
                Flowable.FromPublisher(a[0]).Map(v => combiner(new T[] { v })).Subscribe(subscriber);
                return;
            }

            var parent = new FlowableCombineLatest<T, R>.CombineLatestSubscription(subscriber, combiner, n, prefetch);
            subscriber.OnSubscribe(parent);

            parent.Subscribe(a);
        }
    }
}
