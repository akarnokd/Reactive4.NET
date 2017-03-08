using System;
using System.Threading;
using Reactive.Streams;

namespace Reactive4.NET.operators
{
    sealed class StrictSubscriber<T> : IFlowableSubscriber<T>, ISubscription
    {
        static readonly Exception DefaultError = new Exception("ISubscriber already terminated");

        readonly ISubscriber<T> actual;

        int wip;

        int hasSubscribed;

        Exception error;

        ISubscription upstream;

        long requested;

        internal StrictSubscriber(ISubscriber<T> actual)
        {
            this.actual = actual;
        }

        public void Cancel()
        {
            upstream.Cancel();
        }

        public void OnComplete()
        {
            if (Interlocked.Increment(ref wip) == 1)
            {
                var ex = Interlocked.Exchange(ref error, DefaultError);
                if (ex == null)
                {
                    actual.OnComplete();
                } else {
                    actual.OnError(ex);
                }
            }
        }

        public void OnError(Exception e)
        {
            if (Interlocked.CompareExchange(ref error, e, null) == null)
            {
                if (Interlocked.Increment(ref wip) == 1)
                {
                    actual.OnError(e);
                }
            }
        }

        public void OnNext(T t)
        {
            if (Volatile.Read(ref wip) == 0 && Interlocked.CompareExchange(ref wip, 1, 0) == 0)
            {
                actual.OnNext(t);
                if (Interlocked.CompareExchange(ref wip, 0, 1) != 1)
                {
                    var ex = Interlocked.Exchange(ref error, DefaultError);
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

        public void OnSubscribe(ISubscription subscription)
        {
            if (Interlocked.CompareExchange(ref hasSubscribed, 1, 0) == 0)
            {
                actual.OnSubscribe(this);
                SubscriptionHelper.DeferredSetOnce(ref upstream, ref requested, subscription);
            } else
            {
                subscription.Cancel();
            }
        }

        public void Request(long n)
        {
            if (n <= 0L)
            {
                upstream.Cancel();
                OnError(new InvalidOperationException("§3.9 violated: non-positive request received"));
            } else
            {
                SubscriptionHelper.DeferredRequest(ref upstream, ref requested, n);
            }
        }
    }
}
