using System;
using System.Threading;
using Reactive.Streams;

namespace Reactive4.NET.operators
{
    internal static class SubscriptionHelper
    {
        internal static readonly CancelledSubscription Cancelled = new CancelledSubscription();

        internal static bool Cancel(ref ISubscription field)
        {
            var current = Volatile.Read(ref field);
            if (current != Cancelled)
            {
                current = Interlocked.Exchange(ref field, Cancelled);
                if (current != Cancelled)
                {
                    current?.Cancel();
                    return true;
                }
            }
            return false;
        }

        internal static bool IsCancelled(ref ISubscription field)
        {
            return Volatile.Read(ref field) == Cancelled;
        }

        internal static void LazySetCancel(ref ISubscription field)
        {
            Volatile.Write(ref field, Cancelled);
        }

        internal static long AddRequest(ref long requested, long n)
        {
            for (;;)
            {
                long r = Volatile.Read(ref requested);
                if (r == long.MaxValue)
                {
                    return long.MaxValue;
                }
                long u = r + n;
                if (u < 0L)
                {
                    u = long.MaxValue;
                }
                if (Interlocked.CompareExchange(ref requested, u, r) == r)
                {
                    return r;
                }
            }
        }

        internal static bool SetOnce(ref ISubscription subscription, ISubscription next)
        {
            if (next == null)
            {
                throw new ArgumentNullException(nameof(next));
            }
            ISubscription current = Interlocked.CompareExchange(ref subscription, next, null);
            if (current == null)
            {
                return true;
            }
            if (current != Cancelled)
            {
                throw new InvalidOperationException("ISubscription already set!");
            }
            return false;
        }

        internal static bool DeferredSetOnce(ref ISubscription subscription, ref long requested, ISubscription next)
        {
            if (next == null)
            {
                throw new ArgumentNullException(nameof(next));
            }
            if (Interlocked.CompareExchange(ref subscription, next, null) == null)
            {
                long r = Interlocked.Exchange(ref requested, 0L);
                if (r != 0L)
                {
                    next.Request(r);
                }
                return true;
            }
            next.Cancel();
            return false;
        }

        internal static void DeferredRequest(ref ISubscription subscription, ref long requested, long n)
        {
            if (n <= 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(n));
            }
            var s = Volatile.Read(ref subscription);
            if (s != null)
            {
                s.Request(n);
            }
            else
            {
                AddRequest(ref requested, n);
                s = Volatile.Read(ref subscription);
                if (s != null)
                {
                    long r = Interlocked.Exchange(ref requested, 0L);
                    if (r != 0L)
                    {
                        s.Request(r);
                    }
                }
            }
        }
    }

    internal sealed class CancelledSubscription : ISubscription
    {
        public void Cancel()
        {
            // deliberately no-op
        }

        public void Request(long n)
        {
            // deliberately no-op
        }
    }

    internal sealed class EmptySubscription<T> : IQueueSubscription<T>
    {
        public static readonly IQueueSubscription<T> Instance = new EmptySubscription<T>();

        public void Cancel()
        {
            // deliberately no-op
        }

        public void Clear()
        {
            // deliberately no-op
        }

        public bool IsEmpty()
        {
            return true;
        }

        public bool Offer(T item)
        {
            throw new InvalidOperationException("Should not be called");
        }

        public bool Poll(out T item)
        {
            item = default(T);
            return false;
        }

        public void Request(long n)
        {
            // deliberately no-op
        }

        public int RequestFusion(int mode)
        {
            return FusionSupport.ASYNC;
        }
    }
}
