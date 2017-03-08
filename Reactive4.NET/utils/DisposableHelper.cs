using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Reactive4.NET.utils
{
    internal static class DisposableHelper
    {
        internal static readonly IDisposable Disposed = new DisposedDisposable();

        internal static bool Cancel(ref IDisposable field)
        {
            var current = Volatile.Read(ref field);
            if (current != Disposed)
            {
                current = Interlocked.Exchange(ref field, Disposed);
                if (current != Disposed)
                {
                    current?.Dispose();
                    return true;
                }
            }
            return false;
        }

        internal static bool IsCancelled(ref IDisposable field)
        {
            return Volatile.Read(ref field) == Disposed;
        }

        internal static bool Replace(ref IDisposable field, IDisposable next)
        {
            for (;;)
            {
                var a = Volatile.Read(ref field);
                if (a == Disposed)
                {
                    next?.Dispose();
                    return false;
                }
                if (Interlocked.CompareExchange(ref field, next, a) == a)
                {
                    return true;
                }
            }
        }

        internal static bool Set(ref IDisposable field, IDisposable next)
        {
            for (;;)
            {
                var a = Volatile.Read(ref field);
                if (a == Disposed)
                {
                    next?.Dispose();
                    return false;
                }
                if (Interlocked.CompareExchange(ref field, next, a) == a)
                {
                    a?.Dispose();
                    return true;
                }
            }
        }

        sealed class DisposedDisposable : IDisposable
        {
            public void Dispose()
            {
                // deliberately no-op
            }
        }
    }

    internal sealed class EmptyDisposable : IDisposable
    {
        internal static readonly IDisposable Instance = new EmptyDisposable();

        private EmptyDisposable() { }

        public void Dispose()
        {
            // deliberately no-op
        }
    }
}
