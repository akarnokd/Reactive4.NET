using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Reactive.Streams;
using System.Threading;
using Reactive4.NET.operators;

namespace Reactive4.NET.subscribers
{
    internal abstract class AbstractBlockingSubscriber<T> : IFlowableSubscriber<T>, IDisposable
    {
        protected readonly CountdownEvent latch;

        protected ISubscription upstream;

        protected bool hasItem;
        protected T item;
        protected Exception error;

        internal AbstractBlockingSubscriber()
        {
            latch = new CountdownEvent(1);
        }

        public void Dispose()
        {
            SubscriptionHelper.Cancel(ref upstream);
        }

        public void OnComplete()
        {
            SubscriptionHelper.LazySetCancel(ref upstream);
            latch.Signal();
        }

        public void OnError(Exception cause)
        {
            if (!SubscriptionHelper.IsCancelled(ref upstream))
            {
                item = default(T);
                error = cause;
                SubscriptionHelper.LazySetCancel(ref upstream);
            }
            latch.Signal();
        }

        public abstract void OnNext(T element);

        public void OnSubscribe(ISubscription subscription)
        {
            if (SubscriptionHelper.SetOnce(ref upstream, subscription))
            {
                subscription.Request(long.MaxValue);
            }
        }

        public bool BlockingGet(out T result)
        {
            if (latch.CurrentCount != 0)
            {
                try
                {
                    latch.Wait();
                }
                catch
                {
                    Dispose();
                    throw;
                }
            }

            Exception ex = error;
            if (ex != null)
            {
                throw ex;
            }
            if (hasItem)
            {
                result = item;
                return true;
            }
            result = default(T);
            return false;
        }
    }
}
