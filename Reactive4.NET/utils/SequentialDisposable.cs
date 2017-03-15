using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Reactive4.NET.utils
{
    internal sealed class SequentialDisposable : IDisposable
    {
        IDisposable d;

        internal SequentialDisposable()
        {

        }

        internal SequentialDisposable(IDisposable initial)
        {
            Volatile.Write(ref d, initial);
        }

        public bool IsDisposed()
        {
            return DisposableHelper.IsDisposed(ref d);
        }

        public void Dispose()
        {
            DisposableHelper.Dispose(ref d);
        }

        internal bool Replace(IDisposable next)
        {
            return DisposableHelper.Replace(ref d, next);
        }

        internal bool Set(IDisposable next)
        {
            return DisposableHelper.Set(ref d, next);
        }

    }
}
