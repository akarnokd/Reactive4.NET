using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reactive4.NET.operators
{
    sealed class FlowableRepeat<T> : AbstractFlowableOperator<T, T>
    {
        readonly Func<bool> stop;

        readonly long times;

        public FlowableRepeat(IFlowable<T> source, Func<bool> stop, long times) : base(source)
        {
            this.stop = stop;
            this.times = times;
        }
         
        public override void Subscribe(IFlowableSubscriber<T> subscriber)
        {
            throw new NotImplementedException();
        }
    }
}
