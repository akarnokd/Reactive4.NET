using System;
using System.Collections.Generic;
using System.Text;
using Reactive.Streams;

namespace Reactive4.NET.operators
{
    sealed class FlowableFromPublisher<T> : AbstractFlowableSource<T>
    {
        readonly IPublisher<T> source;

        internal FlowableFromPublisher(IPublisher<T> source)
        {
            this.source = source;
        }

        public override void Subscribe(IFlowableSubscriber<T> subscriber)
        {
            source.Subscribe(subscriber);
        }
    }
}
