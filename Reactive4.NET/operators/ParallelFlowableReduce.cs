using Reactive.Streams;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reactive4.NET.operators
{
    sealed class ParallelFlowableReduce<T, R> : AbstractParallelOperator<T, R>
    {
        readonly Func<R> initialSupplier;

        readonly Func<R, T, R> reducer;

        public ParallelFlowableReduce(IParallelFlowable<T> source, Func<R> initialSupplier, Func<R, T, R> reducer) : base(source)
        {
            this.initialSupplier = initialSupplier;
            this.reducer = reducer;
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
                    R initial;

                    try
                    {
                        initial = initialSupplier();
                    }
                    catch (Exception ex)
                    {
                        foreach (var z in subscribers)
                        {
                            z.OnSubscribe(EmptySubscription<R>.Instance);
                            z.OnError(ex);
                        }
                        return;
                    }

                    parents[i] = new FlowableReduce<T, R>.ReduceSubscriber(s, initial, reducer);
                }

                source.Subscribe(parents);
            }
        }
    }
}
