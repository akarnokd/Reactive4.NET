using Reactive.Streams;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Reactive4.NET.utils
{
    internal static class SerializationHelper
    {
        internal static void OnNext<T>(ISubscriber<T> actual, ref int wip, ref Exception error, T item)
        {
            if (Volatile.Read(ref wip) == 0 && Interlocked.CompareExchange(ref wip, 1, 0) == 0)
            {
                actual.OnNext(item);
                if (Interlocked.CompareExchange(ref wip, 0, 1) != 1)
                {
                    var ex = Interlocked.Exchange(ref error, ExceptionHelper.Terminated);
                    if (ex == null)
                    {
                        actual.OnComplete();
                    }
                    else
                    {
                        actual.OnError(ex);
                    }
                }
            }
        }

        internal static bool TryOnNext<T>(IConditionalSubscriber<T> actual, ref int wip, ref Exception error, T item)
        {
            if (Volatile.Read(ref wip) == 0 && Interlocked.CompareExchange(ref wip, 1, 0) == 0)
            {
                bool b = actual.TryOnNext(item);
                if (Interlocked.CompareExchange(ref wip, 0, 1) != 1)
                {
                    var ex = Interlocked.Exchange(ref error, ExceptionHelper.Terminated);
                    if (ex == null)
                    {
                        actual.OnComplete();
                    }
                    else
                    {
                        actual.OnError(ex);
                    }
                    return false;
                }
                return b;
            }
            return false;
        }

        internal static void OnError<T>(ISubscriber<T> actual, ref int wip, ref Exception error, Exception cause)
        {
            if (Interlocked.CompareExchange(ref error, cause, null) == null)
            {
                if (Interlocked.Increment(ref wip) == 1)
                {
                    actual.OnError(cause);
                }
            }
        }

        internal static void OnComplete<T>(ISubscriber<T> actual, ref int wip, ref Exception error)
        {
            if (Interlocked.Increment(ref wip) == 1)
            {
                var ex = Interlocked.Exchange(ref error, ExceptionHelper.Terminated);
                if (ex == null)
                {
                    actual.OnComplete();
                }
                else
                {
                    actual.OnError(ex);
                }
            }
        }
    }
}
