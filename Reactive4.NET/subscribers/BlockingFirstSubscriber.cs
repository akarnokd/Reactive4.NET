using Reactive4.NET.operators;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reactive4.NET.subscribers
{
    sealed class BlockingFirstSubscriber<T> : AbstractBlockingSubscriber<T>
    {
        public override void OnNext(T element)
        {
            if (!SubscriptionHelper.IsCancelled(ref upstream))
            {
                hasItem = true;
                item = element;
                upstream.Cancel();
                SubscriptionHelper.LazySetCancel(ref upstream);
                latch.Signal();
            }
        }
    }
}
