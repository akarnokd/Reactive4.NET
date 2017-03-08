using System;
using System.Collections.Generic;
using System.Text;

namespace Reactive4.NET
{
    public interface IParallelFlowable<out T>
    {
        int parallelism { get; }

        void Subscribe(IFlowableSubscriber<T>[] subscribers);
    }
}
