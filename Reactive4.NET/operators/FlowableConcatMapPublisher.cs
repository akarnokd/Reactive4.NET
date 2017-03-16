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
    sealed class FlowableConcatMapPublisher<T, R> : AbstractFlowableSource<R>
    {
        readonly IPublisher<T> source;

        readonly Func<T, IPublisher<R>> mapper;

        readonly int prefetch;

        public FlowableConcatMapPublisher(IPublisher<T> source, Func<T, IPublisher<R>> mapper, int prefetch) : base(source)
        {
            this.source = source;
            this.mapper = mapper;
            this.prefetch = prefetch;
        }

        public override void Subscribe(IFlowableSubscriber<R> subscriber)
        {
            source.Subscribe(new FlowableConcatMap<T, R>.ConcatMapSubscriber(subscriber, mapper, prefetch));
        }
    }
}
