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
    sealed class FlowableWithLatestFromEnumerable<T, R> : AbstractFlowableOperator<T, R>
    {
        readonly IEnumerable<IPublisher<T>> others;

        readonly Func<T[], R> combiner;

        public FlowableWithLatestFromEnumerable(IFlowable<T> source, IEnumerable<IPublisher<T>> others, Func<T[], R> combiner) : base(source)
        {
            this.others = others;
            this.combiner = combiner;
        }

        public override void Subscribe(IFlowableSubscriber<R> subscriber)
        {
            var os = others;

            IPublisher<T>[] a = new IPublisher<T>[8];
            int n = 0;

            foreach (var other in os)
            {
                if (n == a.Length)
                {
                    var b = new IPublisher<T>[n + (n >> 2)];
                    Array.Copy(a, 0, b, 0, n);
                    a = b;
                }
                a[n++] = other;
            }

            var parent = new FlowableWithLatestFromArray<T, R>.WithLatestFromArraySubscriber(subscriber, combiner, n);
            subscriber.OnSubscribe(parent);
            for (int i = 0; i < n; i++)
            {
                a[i].Subscribe(parent.others[i]);
            }
            source.Subscribe(parent);
        }
    }
}
