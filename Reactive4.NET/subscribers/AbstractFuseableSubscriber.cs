using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Reactive.Streams;
using Reactive4.NET.operators;

namespace Reactive4.NET.subscribers
{
    internal abstract class AbstractFuseableSubscriber<T, R> : IFlowableSubscriber<T>, IQueueSubscription<R>
    {
        protected ISubscription upstream;

        protected IQueueSubscription<T> qs;

        protected bool done;

        protected int fusionMode;

        public virtual void Cancel()
        {
            upstream.Cancel();
        }

        public void Clear()
        {
            qs.Clear();
        }

        public bool IsEmpty()
        {
            return qs.IsEmpty();
        }

        public bool Offer(R item)
        {
            throw new InvalidOperationException("Should not be called");
        }

        public abstract void OnComplete();

        public abstract void OnError(Exception cause);

        public abstract void OnNext(T element);

        protected abstract void OnStart(ISubscription subscription);

        public void OnSubscribe(ISubscription subscription)
        {
            if (SubscriptionHelper.Validate(ref upstream, subscription))
            {
                if (subscription is IQueueSubscription<T> qs)
                {
                    this.qs = qs;
                }
                OnStart(subscription);
            }
        }

        public abstract bool Poll(out R item);

        public virtual void Request(long n)
        {
            upstream.Request(n);
        }

        public virtual int RequestFusion(int mode)
        {
            var qs = this.qs;
            if (qs != null)
            {
                if ((mode & FusionSupport.BARRIER) == 0)
                {
                    int m = qs.RequestFusion(mode);
                    fusionMode = m;
                    return m;
                }
            }
            return FusionSupport.NONE;
        }

        protected int RequestTransientFusion(int mode)
        {
            var qs = this.qs;
            if (qs != null)
            {
                int m = qs.RequestFusion(mode);
                fusionMode = m;
                return m;
            }
            return FusionSupport.NONE;
        }
    }

    internal abstract class AbstractFuseableConditionalSubscriber<T, R> : AbstractFuseableSubscriber<T, R>, IConditionalSubscriber<T>
    {
        public override void OnNext(T element)
        {
            if (!TryOnNext(element))
            {
                upstream.Request(1);
            }
        }

        public abstract bool TryOnNext(T element);
    }
}
