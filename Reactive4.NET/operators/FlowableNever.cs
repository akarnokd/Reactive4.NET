using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reactive4.NET.operators
{
    sealed class FlowableNever<T> : AbstractFlowableSource<T>
    {
        internal static readonly FlowableNever<T> Instance = new FlowableNever<T>();

        public override void Subscribe(IFlowableSubscriber<T> subscriber)
        {
            subscriber.OnSubscribe(EmptySubscription<T>.Instance);
        }
    }
}
