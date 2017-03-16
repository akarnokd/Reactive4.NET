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
    sealed class TaskIgnoreElementsSubscriber<T> : IFlowableSubscriber<T>
    {
        readonly TaskCompletionSource<object> tcs;

        readonly CancellationTokenRegistration reg;

        internal Task Task
        {
            get
            {
                return tcs.Task;
            }
        }

        ISubscription upstream;

        internal TaskIgnoreElementsSubscriber(CancellationTokenSource cts)
        {
            this.tcs = new TaskCompletionSource<object>();
            reg = cts.Token.Register(Cancel);
        }

        void Cancel()
        {
            SubscriptionHelper.Cancel(ref upstream);
            tcs.TrySetCanceled();
        }

        public void OnComplete()
        {
            reg.Dispose();
            tcs.TrySetResult(null);
        }

        public void OnError(Exception cause)
        {
            reg.Dispose();
            tcs.TrySetException(cause);
        }

        public void OnNext(T element)
        {
            // ignored
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
