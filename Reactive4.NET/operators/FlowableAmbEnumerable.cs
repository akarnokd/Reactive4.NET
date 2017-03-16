using Reactive.Streams;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Reactive4.NET.operators
{
    sealed class FlowableAmbEnumerable<T> : AbstractFlowableSource<T>
    {
        readonly IEnumerable<IPublisher<T>> sources;

        internal FlowableAmbEnumerable(IEnumerable<IPublisher<T>> sources)
        {
            this.sources = sources;
        }

        public override void Subscribe(IFlowableSubscriber<T> subscriber)
        {
            var s = sources;

            var a = new IPublisher<T>[8];

            int n = 0;

            try
            {
                foreach (var p in sources)
                {
                    if (p == null)
                    {
                        throw new NullReferenceException("One of the source IPublishers is null");
                    }
                    if (n == a.Length)
                    {
                        var b = new IPublisher<T>[n + (n >> 2)];
                        Array.Copy(a, 0, b, 0, n);
                        a = b;
                    }
                    a[n++] = p;
                }
            } catch (Exception ex)
            {
                subscriber.OnSubscribe(EmptySubscription<T>.Instance);
                subscriber.OnError(ex);
                return;
            }

            if (n == 0)
            {
                subscriber.OnSubscribe(EmptySubscription<T>.Instance);
                subscriber.OnComplete();
                return;
            }
            if (n == 1)
            {
                a[0].Subscribe(subscriber);
                return;
            }

            var parent = new FlowableAmbArray<T>.AmbSubscription(subscriber, n);
            subscriber.OnSubscribe(parent);
            parent.Subscribe(a);
        }

        
    }
}
