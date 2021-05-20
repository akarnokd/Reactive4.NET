using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Reactive4.NET.utils
{
    internal static class ExceptionHelper
    {
        internal static readonly Exception Terminated = new Exception("ISubscriber already terminated");

        internal static bool AddException(ref Exception error, Exception next)
        {
            for (;;)
            {
                Exception ex = Volatile.Read(ref error);
                if (ex == Terminated)
                {
                    return false;
                }
                Exception b;
                if (ex != null)
                {
                    if (ex is AggregateException aggregateException)
                    {
                        b = new AggregateException(aggregateException.InnerExceptions.Concat(new[] { next }));
                    }
                    else
                    {
                        b = new AggregateException(ex, next);
                    }
                }
                else
                {
                    b = next;
                }
                if (Interlocked.CompareExchange(ref error, b, ex) == ex)
                {
                    return true;
                }
            }
        }

        internal static Exception Terminate(ref Exception error)
        {
            Exception ex = Volatile.Read(ref error);
            if (ex == Terminated)
            {
                return Interlocked.Exchange(ref error, Terminated);
            }
            return ex;
        }
    }
}
