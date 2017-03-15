using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reactive4.NET.operators
{
    sealed class FlowableLift<T, R> : AbstractFlowableOperator<T, R>
    {
        readonly Func<IFlowableSubscriber<R>, IFlowableSubscriber<T>> lifter;

        public FlowableLift(IFlowable<T> source, Func<IFlowableSubscriber<R>, IFlowableSubscriber<T>> lifter) : base(source)
        {
            this.lifter = lifter;
        }

        public override void Subscribe(IFlowableSubscriber<R> subscriber)
        {
            IFlowableSubscriber<T> s;

            try
            {
                s = lifter(subscriber);
                if (s == null)
                {
                    throw new NullReferenceException("The lifter returned a null IFlowableSubscriber");
                }
            }
            catch (Exception ex)
            {
                subscriber.OnSubscribe(EmptySubscription<R>.Instance);
                subscriber.OnError(ex);
                return;
            }

            source.Subscribe(s);
        }
    }
}
