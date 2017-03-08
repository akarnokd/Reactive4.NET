using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reactive4.NET.operators
{
    internal sealed class FlowableEmpty<T> : AbstractFlowableSource<T>, IConstantSource<T>
    {

        internal static readonly IFlowable<T> Instance = new FlowableEmpty<T>();

        FlowableEmpty()
        {

        }

        public override void Subscribe(IFlowableSubscriber<T> subscriber)
        {
            subscriber.OnSubscribe(EmptySubscription<T>.Instance);
            subscriber.OnComplete();
        }

        public bool Value(out T value)
        {
            value = default(T);
            return false;
        }
    }
}
