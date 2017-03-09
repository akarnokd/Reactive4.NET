using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Reactive4.NET.utils
{
    internal abstract class AbstractDeferredScalarSubscription<T> : IQueueSubscription<T>
    {
        internal static readonly int STATE_NO_REQUEST_NO_VALUE = 0;
        internal static readonly int STATE_HAS_REQUEST_NO_VALUE = 1;
        internal static readonly int STATE_NO_REQUEST_HAS_VALUE = 2;
        internal static readonly int STATE_HAS_REQUEST_HAS_VALUE = 3;
        internal static readonly int STATE_CANCELLED = 4;
        internal static readonly int STATE_FUSED = 5;
        internal static readonly int STATE_HAS_VALUE = 6;
        internal static readonly int STATE_CONSUMED = 7;

        protected readonly IFlowableSubscriber<T> actual;

        protected int state;

        protected T value;

        internal AbstractDeferredScalarSubscription(IFlowableSubscriber<T> actual)
        {
            this.actual = actual;
        }

        public virtual void Cancel()
        {
            Volatile.Write(ref state, STATE_CANCELLED);
        }

        public void Clear()
        {
            value = default(T);
            Volatile.Write(ref state, STATE_CONSUMED);
        }

        public bool IsEmpty()
        {
            return Volatile.Read(ref state) != STATE_HAS_VALUE;
        }

        public bool Offer(T item)
        {
            throw new InvalidOperationException("Should not be called");
        }

        public bool Poll(out T item)
        {
            if (Volatile.Read(ref state) == STATE_HAS_VALUE)
            {
                Volatile.Write(ref state, STATE_CONSUMED);
                item = value;
                return true;
            }
            item = default(T);
            return false;
        }

        public void Request(long n)
        {
            for (;;)
            {
                var s = Volatile.Read(ref state);
                if (s == STATE_NO_REQUEST_HAS_VALUE)
                {
                    if (Interlocked.CompareExchange(ref state, STATE_HAS_REQUEST_HAS_VALUE, STATE_NO_REQUEST_HAS_VALUE) == STATE_NO_REQUEST_HAS_VALUE)
                    {
                        actual.OnNext(value);
                        if (Volatile.Read(ref state) != STATE_CANCELLED)
                        {
                            actual.OnComplete();
                        }
                        break;
                    }
                }
                if (s == STATE_NO_REQUEST_NO_VALUE)
                {
                    if (Interlocked.CompareExchange(ref state, STATE_HAS_REQUEST_NO_VALUE, STATE_NO_REQUEST_NO_VALUE) == STATE_NO_REQUEST_NO_VALUE)
                    {
                        break;
                    }
                }
                else
                {
                    break;
                }
            }
        }

        public int RequestFusion(int mode)
        {
            int m = mode & FusionSupport.ASYNC;
            if (m != 0)
            {
                Volatile.Write(ref state, STATE_FUSED);
            }
            return m;
        }

        public bool IsCancelled()
        {
            return Volatile.Read(ref state) == STATE_CANCELLED;
        }

        public void Complete()
        {
            if (Volatile.Read(ref state) != STATE_CANCELLED)
            {
                Volatile.Write(ref state, STATE_CONSUMED);
                actual.OnComplete();
            }
        }

        public void Complete(T item)
        {
            for (;;)
            {
                var s = Volatile.Read(ref state);
                if (s == STATE_FUSED)
                {
                    value = item;
                    Volatile.Write(ref state, STATE_HAS_VALUE);
                    actual.OnNext(default(T));
                    if (Volatile.Read(ref state) != STATE_CANCELLED)
                    {
                        actual.OnComplete();
                    }
                    break;
                }
                else if (s == STATE_NO_REQUEST_NO_VALUE)
                {
                    value = item;
                    if (Interlocked.CompareExchange(ref state, STATE_NO_REQUEST_HAS_VALUE, STATE_NO_REQUEST_NO_VALUE) == STATE_NO_REQUEST_NO_VALUE)
                    {
                        break;
                    }
                }
                else if (s == STATE_HAS_REQUEST_NO_VALUE)
                {
                    Volatile.Write(ref state, STATE_HAS_REQUEST_HAS_VALUE);
                    actual.OnNext(item);
                    if (Volatile.Read(ref state) != STATE_CANCELLED)
                    {
                        actual.OnComplete();
                    }
                    break;
                }
                else
                {
                    break;
                }
            }
        }
    }
}
