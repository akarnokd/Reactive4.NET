using Reactive.Streams;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reactive4.NET.operators
{
    sealed class FlowableDefer<T> : AbstractFlowableSource<T>
    {
        readonly Func<IPublisher<T>> supplier;

        internal FlowableDefer(Func<IPublisher<T>> supplier)
        {
            this.supplier = supplier;
        }

        public override void Subscribe(IFlowableSubscriber<T> subscriber)
        {
            IPublisher<T> p;

            try
            {
                p = supplier();
                if (p == null)
                {
                    throw new NullReferenceException("The supplier returned a null IPublisher");
                }
            }
            catch (Exception ex)
            {
                subscriber.OnSubscribe(EmptySubscription<T>.Instance);
                subscriber.OnError(ex);
                return;
            }

            p.Subscribe(subscriber);
        }
    }
}
