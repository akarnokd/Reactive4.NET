using Reactive.Streams;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reactive4.NET.operators
{
    sealed class ParallelFlowableArray<T> : AbstractParallelSource<T>
    {
        readonly IPublisher<T>[] sources;

        public override int Parallelism => sources.Length;

        internal ParallelFlowableArray(IPublisher<T>[] sources)
        {
            this.sources = sources;
        }

        public override void Subscribe(IFlowableSubscriber<T>[] subscribers)
        {
            if (Validate(subscribers))
            {
                var srcs = sources;
                int n = subscribers.Length;

                for (int i = 0; i < n; n++)
                {
                    srcs[i].Subscribe(subscribers[i]);
                }
            }
        }
    }
}
