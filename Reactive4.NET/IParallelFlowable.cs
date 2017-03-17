using System;
using System.Collections.Generic;
using System.Text;

namespace Reactive4.NET
{
    /// <summary>
    /// Represents a parallel execution-capable backpressured type.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    public interface IParallelFlowable<out T>
    {
        /// <summary>
        /// The parallelism level of this IParallelFlowable, positive.
        /// </summary>
        int Parallelism { get; }

        /// <summary>
        /// Subscribe with an array of IFlowableSubscribers whose number
        /// must match the Parallelism level of this IParallelFlowable.
        /// </summary>
        /// <param name="subscribers">The array of subscribers to subscribe with.</param>
        void Subscribe(IFlowableSubscriber<T>[] subscribers);
    }
}
