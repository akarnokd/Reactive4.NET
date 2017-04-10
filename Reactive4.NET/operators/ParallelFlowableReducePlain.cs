using Reactive.Streams;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reactive4.NET.operators
{
    sealed class ParallelFlowableReducePlain<T> : AbstractParallelOperator<T, T>
    {
        readonly Func<T, T, T> reducer;

        public ParallelFlowableReducePlain(IParallelFlowable<T> source, Func<T, T, T> reducer) : base(source)
        {
            this.reducer = reducer;
        }

        public override void Subscribe(IFlowableSubscriber<T>[] subscribers)
        {
            if (Validate(subscribers))
            {
                int n = subscribers.Length;
                var parents = new IFlowableSubscriber<T>[n];

                for (int i = 0; i < n; i++)
                {
                    var s = subscribers[i];
                    parents[i] = new FlowableReducePlain<T>.ReducePlainSubscriber(s, reducer);
                }

                source.Subscribe(parents);
            }
        }
    }
}
