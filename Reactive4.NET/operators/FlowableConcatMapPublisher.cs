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

        readonly ErrorMode errorMode;

        public FlowableConcatMapPublisher(IPublisher<T> source, Func<T, IPublisher<R>> mapper, int prefetch, ErrorMode errorMode)
        {
            this.source = source;
            this.mapper = mapper;
            this.prefetch = prefetch;
            this.errorMode = errorMode;
        }

        public override void Subscribe(IFlowableSubscriber<R> subscriber)
        {
            if (errorMode == ErrorMode.Immediate)
            {
                source.Subscribe(new FlowableConcatMap<T, R>.ConcatMapSubscriberEagerError(subscriber, mapper, prefetch));
            }
            else
            {
                source.Subscribe(new FlowableConcatMap<T, R>.ConcatMapSubscriber(subscriber, mapper, prefetch, errorMode == ErrorMode.End));
            }
        }
    }
}
