using System;
using System.Collections.Generic;
using System.Text;
using Reactive.Streams;

namespace Reactive4.NET.operators
{
    internal abstract class AbstractParallelSource<T> : IParallelFlowable<T>
    {
        public abstract int Parallelism { get; }

        public abstract void Subscribe(IFlowableSubscriber<T>[] subscribers);

        internal bool Validate(IFlowableSubscriber<T>[] subscribers)
        {
            if (Parallelism != subscribers.Length)
            {
                var ex = new ArgumentOutOfRangeException(nameof(subscribers), "The number of subscribers (" + subscribers.Length + ") differs from the parallelism level (" + Parallelism + ") of the source.");
                foreach (var s in subscribers)
                {
                    s.OnSubscribe(EmptySubscription<T>.Instance);
                    s.OnError(ex);
                }
                return false;
            }
            return true;
        }
    }

    internal abstract class AbstractParallelOperator<T, R> : AbstractParallelSource<R>
    {
        internal readonly IParallelFlowable<T> source;

        internal readonly int parallelism;

        internal AbstractParallelOperator(IParallelFlowable<T> source)
        {
            this.source = source;
            this.parallelism = source.Parallelism;
        }

        public override int Parallelism => parallelism;
    }
}
