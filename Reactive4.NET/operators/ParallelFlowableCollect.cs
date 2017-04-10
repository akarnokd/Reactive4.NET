using Reactive.Streams;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reactive4.NET.operators
{
    sealed class ParallelFlowableCollect<T, R> : AbstractParallelOperator<T, R>
    {
        readonly Func<R> initialSupplier;

        readonly Action<R, T> collector;

        public ParallelFlowableCollect(IParallelFlowable<T> source, Func<R> initialSupplier, Action<R, T> collector) : base(source)
        {
            this.initialSupplier = initialSupplier;
            this.collector = collector;
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

                    parents[i] = new FlowableCollect<T, R>.CollectSubscriber(s, initial, collector);
                }

                source.Subscribe(parents);
            }
        }
    }
}
