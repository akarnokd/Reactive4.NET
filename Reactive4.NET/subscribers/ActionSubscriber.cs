using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Reactive.Streams;
using Reactive4.NET.operators;
using System.Threading;

namespace Reactive4.NET.subscribers
{
    internal sealed class ActionSubscriber<T> : IFlowableSubscriber<T>, IDisposable
    {
        readonly Action<T> onNext;

        readonly Action<Exception> onError;

        readonly Action onComplete;

        ISubscription upstream;

        internal ActionSubscriber(Action<T> onNext, Action<Exception> onError, Action onComplete)
        {
            this.onNext = onNext;
            this.onError = onError;
            this.onComplete = onComplete;
        }

        public void Dispose()
        {
            SubscriptionHelper.Cancel(ref upstream);
        }

        public void OnComplete()
        {
            if (SubscriptionHelper.IsCancelled(ref upstream))
            {
                return;
            }
            Volatile.Write(ref upstream, SubscriptionHelper.Cancelled);
            try
            {
                onComplete();
            }
            catch
            {
                // TODO what should happen?
            }
        }

        public void OnError(Exception cause)
        {
            if (SubscriptionHelper.IsCancelled(ref upstream))
            {
                return;
            }
            Volatile.Write(ref upstream, SubscriptionHelper.Cancelled);
            try
            {
                onError(cause);
            }
            catch
            {
                // TODO what should happen?
            }
        }

        public void OnNext(T element)
        {
            if (SubscriptionHelper.IsCancelled(ref upstream))
            {
                return;
            }

            try
            {
                onNext(element);
            }
            catch (Exception ex)
            {
                upstream.Cancel();
                onError(ex);
            }
        }

        public void OnSubscribe(ISubscription subscription)
        {
            if (SubscriptionHelper.SetOnce(ref upstream, subscription))
            {
                subscription.Request(long.MaxValue);
            }
        }
    }
}
