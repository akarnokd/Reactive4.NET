using System;
using System.Collections.Generic;
using System.Text;

namespace Reactive4.NET
{
    public interface IGeneratorEmitter<in T>
    {
        void OnNext(T item);

        void OnError(Exception error);

        void OnComplete();
    }
}
