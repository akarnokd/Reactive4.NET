using Reactive.Streams;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reactive4.NET.operators
{
    sealed class ParallelFlowableFlatMap<T, R> : AbstractParallelOperator<T, R>
    {
        readonly Func<T, IPublisher<R>> mapper;

        readonly int maxConcurrency;

        readonly int bufferSize;

        public ParallelFlowableFlatMap(IParallelFlowable<T> source, Func<T, IPublisher<R>> mapper, int maxConcurrency, int bufferSize) : base(source)
        {
            this.mapper = mapper;
            this.maxConcurrency = maxConcurrency;
            this.bufferSize = bufferSize;
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
                    parents[i] = new FlowableFlatMap<T, R>.FlatMapMainSubscriber(s, mapper, maxConcurrency, bufferSize);
                }

                source.Subscribe(parents);
            }
        }
    }
}
