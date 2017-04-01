using System;
using System.Threading;
using Reactive.Streams;

namespace Reactive4.NET.operators
{
    /// <summary>
    /// Helper utility methods for working with ISubscribers and requests.
    /// </summary>
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

        internal static long MultiplyCap(long a, long b) {
            long u = a * b;
            if (((a | b) >> 31) != 0)
            {
                if (u / a != b)
                {
                    return long.MaxValue;
                }
            }
            return u;
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

        internal static long Produced(ref long requested, long n)
        {
            for (;;)
            {
                long r = Volatile.Read(ref requested);
                if (r == long.MaxValue)
                {
                    return long.MaxValue;
                }
                long u = r - n;
                if (u < 0L)
                {
                    throw new ArgumentOutOfRangeException("More produced than requested: " + u);
                }
                if (Interlocked.CompareExchange(ref requested, u, r) == r)
                {
                    return u;
                }
            }
        }

        internal static bool Validate(long n)
        {
            if (n <= 0)
            {
                throw new ArgumentOutOfRangeException("n > 0 required but it was " + n);
            }
            return true;
        }

        internal static bool Validate(ref ISubscription subscription, ISubscription next)
        {
            if (next == null)
            {
                throw new ArgumentNullException(nameof(next));
            }
            if (subscription != null)
            {
                throw new InvalidOperationException("ISubscription already set!");
            }
            subscription = next;
            return true;
        }

        internal static bool SetOnce(ref ISubscription subscription, ISubscription next, bool crash = true)
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
            next?.Cancel();
            if (current != Cancelled)
            {
                var e = new InvalidOperationException("ISubscription already set!");
                if (crash)
                {
                    throw e;
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine(e.ToString());
                }
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

        internal static bool PostCompleteSingleRequest<T>(IFlowableSubscriber<T> actual, ref long requested, ref T item, long n, ref bool cancelled)
        {
            for (;;)
            {
                var r = Volatile.Read(ref requested);
                if ((r & long.MinValue) != 0L)
                {
                    var u = r & long.MaxValue;
                    var v = long.MinValue + 1;
                    if (Interlocked.CompareExchange(ref requested, v, r) == r)
                    {
                        if (u == 0L && !Volatile.Read(ref cancelled))
                        {
                            actual.OnNext(item);
                            if (!Volatile.Read(ref cancelled))
                            {
                                actual.OnComplete();
                            }
                        }
                        return true;
                    }
                }
                else
                {
                    var v = r + n;
                    if (v < 0L)
                    {
                        v = long.MaxValue;
                    }
                    if (Interlocked.CompareExchange(ref requested, v, r) == r)
                    {
                        return false;
                    }
                }
            }
        }

        internal static void PostCompleteSingleResult<T>(IFlowableSubscriber<T> actual, ref long requested, ref T item, T value, ref bool cancelled)
        {
            for (;;)
            {
                long r = Volatile.Read(ref requested);
                if ((r & long.MinValue) != 0)
                {
                    return;
                }
                long u = r | long.MinValue;
                if (r == 0L)
                {
                    item = value;
                }
                if (Interlocked.CompareExchange(ref requested, u, r) == r)
                {
                    if (r != 0L && !Volatile.Read(ref cancelled))
                    {
                        actual.OnNext(value);
                        if (!Volatile.Read(ref cancelled))
                        {
                            actual.OnComplete();
                        }
                    }
                    return;
                }
            }
        }

        internal static bool PostCompleteMultiRequest<T>(IFlowableSubscriber<T> actual, ref long requested, ISimpleQueue<T> q, long n, ref bool cancelled)
        {
            for (;;)
            {
                long r = Volatile.Read(ref requested);
                if ((r & long.MinValue) != 0L)
                {
                    long u = r & long.MaxValue;
                    long v = u + n;
                    if (v < 0L)
                    {
                        v = long.MaxValue;
                    }
                    v = v | long.MinValue;
                    if (Interlocked.CompareExchange(ref requested, v, r) == r)
                    {
                        if (u == 0)
                        {
                            PostCompleteMultiDrain(actual, ref requested, q, ref cancelled);
                        }
                        return true;
                    }
                }
                else
                {
                    long u = r + n;
                    if (u < 0L)
                    {
                        u = long.MaxValue;
                    }
                    if (Interlocked.CompareExchange(ref requested, u, r) == r)
                    {
                        return false;
                    }
                }
            }
        }
        internal static void PostCompleteMultiResult<T>(IFlowableSubscriber<T> actual, ref long requested, ISimpleQueue<T> q, ref bool cancelled)
        {
            for (;;)
            {
                long r = Volatile.Read(ref requested);
                if ((r & long.MinValue) != 0L)
                {
                    return;
                }
                long u = r | long.MinValue;
                if (Interlocked.CompareExchange(ref requested, u, r) == r)
                {
                    if (r != 0L)
                    {
                        PostCompleteMultiDrain(actual, ref requested, q, ref cancelled);
                    }
                    return;
                }
            }
        }

        static void PostCompleteMultiDrain<T>(IFlowableSubscriber<T> actual, ref long requested, ISimpleQueue<T> q, ref bool cancelled)
        {
            long r = Volatile.Read(ref requested);
            long e = long.MinValue;
            for (;;)
            {
                while (e != r)
                {
                    if (Volatile.Read(ref cancelled))
                    {
                        return;
                    }

                    bool empty = !q.Poll(out T v);

                    if (empty)
                    {
                        actual.OnComplete();
                        return;
                    }

                    actual.OnNext(v);

                    e++;
                }

                if (e == r)
                {
                    if (Volatile.Read(ref cancelled))
                    {
                        return;
                    }
                    if (q.IsEmpty())
                    {
                        actual.OnComplete();
                        return;
                    }
                }

                r = Volatile.Read(ref requested);
                if (r == e)
                {
                    r = Interlocked.Add(ref requested, -(e & long.MaxValue));
                    
                    if (r == long.MinValue)
                    {
                        break;
                    }
                    e = long.MinValue;
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
