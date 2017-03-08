using System;
using System.Collections.Generic;
using System.Text;

namespace Reactive4.NET
{
    public interface IFlowableEmitter<T> : IGeneratorEmitter<T>
    {
        void OnCancel(Action action);

        void OnCancel(IDisposable disposable);

        long Requested { get; }

        bool IsCancelled { get; }
    }
}
