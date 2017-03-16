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
    sealed class BlockingSubscriber<T> : IFlowableSubscriber<T>, IDisposable, ISubscription
    {
        readonly SpscArrayQueue<T> queue;

        readonly int bufferSize;

        readonly int limit;

        readonly IFlowableSubscriber<T> actual;

        ISubscription upstream;

        bool done;
        Exception error;

        int wip;

        long requested;

        internal BlockingSubscriber(int bufferSize, IFlowableSubscriber<T> actual)
        {
            this.actual = actual;
            this.queue = new SpscArrayQueue<T>(bufferSize);
            this.bufferSize = bufferSize;
            this.limit = bufferSize - (bufferSize >> 2);
        }

        public void Dispose()
        {
            SubscriptionHelper.Cancel(ref upstream);
            Unblock();
        }

        public void Run()
        {
            var a = actual;
            var q = queue;
            long e = 0L;
            int consumed = 0;
            int lim = limit;
            for (;;)
            {
                if (SubscriptionHelper.IsCancelled(ref upstream))
                {
                    q.Clear();
                    return;
                }
                if (Volatile.Read(ref wip) == 0)
                {
                    Monitor.Enter(this);
                    try
                    {
                        while (Volatile.Read(ref wip) == 0 && SubscriptionHelper.IsCancelled(ref upstream))
                        {
                            Monitor.Wait(this);
                        }
                    }
                    finally
                    {
                        Monitor.Exit(this);
                    }
                }
                if (SubscriptionHelper.IsCancelled(ref upstream))
                {
                    q.Clear();
                    return;
                }

                bool d = Volatile.Read(ref done);
                bool empty = q.IsEmpty();

                if (d && empty)
                {
                    var ex = error;
                    if (ex != null)
                    {
                        a.OnError(ex);
                    }
                    else
                    {
                        a.OnComplete();
                    }
                    return;
                }

                if (!empty)
                {
                    if (e != Volatile.Read(ref requested))
                    {
                        q.Poll(out T v);

                        a.OnNext(v);

                        e++;
                        if (++consumed == lim)
                        {
                            consumed = 0;
                            upstream.Request(lim);
                        }    
                    }
                }
                Interlocked.Decrement(ref wip);
            }
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
            Volatile.Write(ref done, true);
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
                actual.OnSubscribe(this);

                subscription.Request(bufferSize);
            }
        }

        public void Request(long n)
        {
            if (SubscriptionHelper.Validate(n))
            {
                SubscriptionHelper.AddRequest(ref requested, n);
                Unblock();
            }
        }

        public void Cancel()
        {
            Dispose();
        }
    }
}
