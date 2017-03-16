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
    sealed class TaskLastSubscriber<T> : IFlowableSubscriber<T>
    {
        readonly TaskCompletionSource<T> tcs;

        readonly CancellationTokenRegistration reg;

        internal Task<T> Task
        {
            get
            {
                return tcs.Task;
            }
        }

        ISubscription upstream;

        T last;
        bool hasValue;

        bool done;

        internal TaskLastSubscriber(CancellationTokenSource cts)
        {
            this.tcs = new TaskCompletionSource<T>();
            reg = cts.Token.Register(Cancel);
        }

        void Cancel()
        {
            SubscriptionHelper.Cancel(ref upstream);
            tcs.TrySetCanceled();
        }

        public void OnComplete()
        {
            if (!done)
            {
                done = true;
                reg.Dispose();
                if (hasValue)
                {
                    tcs.TrySetResult(last);
                }
                else
                {
                    tcs.TrySetException(new IndexOutOfRangeException());
                }
            }
        }

        public void OnError(Exception cause)
        {
            if (!done)
            {
                done = true;
                reg.Dispose();
                tcs.TrySetException(cause);
            }
        }

        public void OnNext(T element)
        {
            last = element;
            if (!hasValue)
            {
                hasValue = true;
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
