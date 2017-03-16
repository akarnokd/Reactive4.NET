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
    sealed class BlockingLambdaSubscriber<T> : IFlowableSubscriber<T>, IDisposable
    {
        readonly SpscArrayQueue<T> queue;

        readonly int bufferSize;

        readonly int limit;

        readonly Action<T> onNext;

        readonly Action<Exception> onError;

        readonly Action onComplete;

        ISubscription upstream;

        Exception error;

        int wip;

        int consumed;

        internal BlockingLambdaSubscriber(int bufferSize, Action<T> onNext, Action<Exception> onError, Action onComplete)
        {
            this.onNext = onNext;
            this.onError = onError;
            this.onComplete = onComplete;
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
            for (;;)
            {
                if (SubscriptionHelper.IsCancelled(ref upstream))
                {
                    return;
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
                                return;
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
                if (queue.Poll(out T current))
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
                    onNext(current);
                    Interlocked.Decrement(ref wip);
                    continue;
                }
                var ex = error;
                if (ex != null)
                {
                    onError(ex);
                }
                else
                {
                    onComplete();
                }
                return;
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
    }
}
