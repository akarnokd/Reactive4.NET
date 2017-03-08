using System;
using System.Collections.Generic;
using System.Text;
using Reactive.Streams;

namespace Reactive4.NET.operators
{
    internal abstract class AbstractFlowableSource<T> : IFlowable<T>
    {
        public abstract void Subscribe(IFlowableSubscriber<T> subscriber);

        public void Subscribe(ISubscriber<T> subscriber)
        {
            if (subscriber == null)
            {
                throw new ArgumentNullException(nameof(subscriber));
            }
            if (subscriber is IFlowableSubscriber<T> s)
            {
                Subscribe(s);
            }
            else
            {
                Subscribe(new StrictSubscriber<T>(subscriber));
            }
        }
    }

    internal abstract class AbstractFlowableOperator<T, R> : AbstractFlowableSource<R>, IHasSource<T>
    {
        internal readonly IFlowable<T> source;

        internal AbstractFlowableOperator(IFlowable<T> source)
        {
            this.source = source;
        }

        public IFlowable<T> Source => source;
    }
}
