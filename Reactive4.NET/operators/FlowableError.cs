using System;
using System.Collections.Generic;
using System.Text;

namespace Reactive4.NET.operators
{
    internal sealed class FlowableError<T> : AbstractFlowableSource<T>, IConstantSource<T>
    {
        readonly Exception error;

        internal FlowableError(Exception error)
        {
            this.error = error;
        }

        public override void Subscribe(IFlowableSubscriber<T> subscriber)
        {
            subscriber.OnSubscribe(EmptySubscription<T>.Instance);
            subscriber.OnError(error);
        }

        public bool Value(out T value)
        {
            throw error;
        }
    }
}
