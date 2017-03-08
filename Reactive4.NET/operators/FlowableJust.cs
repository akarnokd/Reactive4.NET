using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace Reactive4.NET.operators
{
    internal sealed class FlowableJust<T> : AbstractFlowableSource<T>, IConstantSource<T>
    {
        readonly T item;

        internal FlowableJust(T item)
        {
            this.item = item;
        }


        public override void Subscribe(IFlowableSubscriber<T> subscriber)
        {
            subscriber.OnSubscribe(new ConstantSubscription(subscriber, item));
        }

        public bool Value(out T value)
        {
            value = item;
            return true;
        }

        internal sealed class ConstantSubscription : IQueueSubscription<T>
        {
            readonly IFlowableSubscriber<T> actual;

            readonly T item;

            int state;

            static readonly int STATE_NONE = 0;
            static readonly int STATE_REQUESTED = 1;
            static readonly int STATE_COMPLETED = 2;
            static readonly int STATE_FUSED = 3;
            static readonly int STATE_CONSUMED = 4;
            static readonly int STATE_CANCELLED = 5;

            internal ConstantSubscription(IFlowableSubscriber<T> actual, T item)
            {
                this.actual = actual;
                this.item = item;
            }

            public void Cancel()
            {
                Volatile.Write(ref state, STATE_CANCELLED);
            }

            public void Clear()
            {
                Volatile.Write(ref state, STATE_CONSUMED);
            }

            public bool IsEmpty()
            {
                return Volatile.Read(ref state) != STATE_FUSED;
            }

            public bool Offer(T item)
            {
                throw new InvalidOperationException("Should not be called!");
            }

            public bool Poll(out T item)
            {
                if (Volatile.Read(ref state) == STATE_FUSED)
                {
                    Volatile.Write(ref state, STATE_CONSUMED);
                    item = this.item;
                    return true;
                }
                item = default(T);
                return false;
            }

            public void Request(long n)
            {
                if (n >= 0L)
                {
                    if (Interlocked.CompareExchange(ref state, STATE_REQUESTED, STATE_NONE) == STATE_NONE)
                    {
                        actual.OnNext(item);
                        if (Volatile.Read(ref state) != STATE_CANCELLED)
                        {
                            Volatile.Write(ref state, STATE_COMPLETED);
                            actual.OnComplete();
                        }
                    }
                }
                else
                {
                    throw new ArgumentOutOfRangeException(nameof(n));
                }
            }

            public int RequestFusion(int mode)
            {
                if ((mode & FusionSupport.SYNC) != 0)
                {
                    Volatile.Write(ref state, STATE_FUSED);
                    return FusionSupport.SYNC;
                }
                return FusionSupport.NONE;
            }
        }
    }
}
