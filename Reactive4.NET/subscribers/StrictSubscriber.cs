using System;
using System.Threading;
using Reactive.Streams;
using Reactive4.NET.utils;

namespace Reactive4.NET.operators
{
    internal sealed class StrictSubscriber<T> : IFlowableSubscriber<T>, ISubscription
    {
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
            SerializationHelper.OnComplete<T>(actual, ref wip, ref error);
        }

        public void OnError(Exception e)
        {
            SerializationHelper.OnError<T>(actual, ref wip, ref error, e);
        }

        public void OnNext(T t)
        {
            SerializationHelper.OnNext<T>(actual, ref wip, ref error, t);          
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
                OnError(new ArgumentException("§3.9 violated: non-positive request received"));
            } else
            {
                SubscriptionHelper.DeferredRequest(ref upstream, ref requested, n);
            }
        }
    }
}
