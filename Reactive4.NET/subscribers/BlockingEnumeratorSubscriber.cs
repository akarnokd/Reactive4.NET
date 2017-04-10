using Reactive4.NET.utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Reactive.Streams;
using System.Collections;
using Reactive4.NET.operators;
using System.Threading;

namespace Reactive4.NET.subscribers
{
    sealed class BlockingEnumeratorSubscriber<T> : IFlowableSubscriber<T>, IEnumerator<T>
    {
        readonly SpscArrayQueue<T> queue;

        readonly int bufferSize;

        readonly int limit;

        ISubscription upstream;

        T current;
        Exception error;

        int wip;

        int consumed;

        internal BlockingEnumeratorSubscriber(int bufferSize)
        {
            queue = new SpscArrayQueue<T>(bufferSize);
            this.bufferSize = bufferSize;
            this.limit = bufferSize - (bufferSize >> 2);
        }

        public T Current => current;

        object IEnumerator.Current => current;

        public void Dispose()
        {
            SubscriptionHelper.Cancel(ref upstream);
            Unblock();
        }

        public bool MoveNext()
        {
            if (SubscriptionHelper.IsCancelled(ref upstream))
            {
                return false;
            }
            if (Volatile.Read(ref wip) == 0)
            {
                Monitor.Enter(this);
                try
                {
                    for (;;)
                    {
                        if (SubscriptionHelper.IsCancelled(ref upstream))
                        {
                            return false;
                        }

                        if (Volatile.Read(ref wip) != 0)
                        {
                            break;
                        }
                        Monitor.Wait(this);
                    }
                }
                finally
                {
                    Monitor.Exit(this);
                }
            }
            if (queue.Poll(out current))
            {
                int c = consumed + 1;
                if (c == limit)
                {
                    consumed = 0;
                    upstream.Request(c);
                }
                else
                {
                    consumed = c;
                }
                Interlocked.Decrement(ref wip);
                return true;
            }
            var ex = error;
            if (ex != null)
            {
                throw ex;
            }
            return false;
        }

        void Unblock()
        {
            if (Interlocked.Increment(ref wip) == 1)
            {
                Monitor.Enter(this);
                Monitor.Pulse(this);
                Monitor.Exit(this);
            }
        }

        public void OnComplete()
        {
            Unblock();
        }

        public void OnError(Exception cause)
        {
            error = cause;
            Unblock();
        }

        public void OnNext(T element)
        {
            queue.Offer(element);
            Unblock();
        }

        public void OnSubscribe(ISubscription subscription)
        {
            if (SubscriptionHelper.SetOnce(ref upstream, subscription))
            {
                subscription.Request(bufferSize);
            }
        }

        public void Reset()
        {
            throw new InvalidOperationException("This IEnumerator doesn't support Reset().");
        }
    }
}
