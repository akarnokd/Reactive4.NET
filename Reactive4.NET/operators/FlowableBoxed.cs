using Reactive4.NET.subscribers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Reactive.Streams;

namespace Reactive4.NET.operators
{
    sealed class FlowableBoxed<T> : AbstractFlowableSource<object>
    {
        readonly IPublisher<T> source;

        public FlowableBoxed(IPublisher<T> source)
        {
            this.source = source;
        }

        public override void Subscribe(IFlowableSubscriber<object> subscriber)
        {
            if (subscriber is IConditionalSubscriber<object> s)
            {
                source.Subscribe(new BoxedConditionalSubscriber(s));
            }
            else
            {
                source.Subscribe(new BoxedSubscriber(subscriber));
            }
        }

        sealed class BoxedSubscriber : AbstractFuseableSubscriber<T, object>
        {
            readonly IFlowableSubscriber<object> actual;

            internal BoxedSubscriber(IFlowableSubscriber<object> actual)
            {
                this.actual = actual;
            }

            public override void OnComplete()
            {
                actual.OnComplete();
            }

            public override void OnError(Exception cause)
            {
                actual.OnError(cause);
            }

            public override void OnNext(T element)
            {
                actual.OnNext(element);
            }

            public override bool Poll(out object item)
            {
                if (qs.Poll(out T v))
                {
                    item = (object)v;
                    return true;
                }
                item = null;
                return false;
            }

            protected override void OnStart(ISubscription subscription)
            {
                actual.OnSubscribe(this);
            }
        }
        sealed class BoxedConditionalSubscriber : AbstractFuseableConditionalSubscriber<T, object>
        {
            readonly IConditionalSubscriber<object> actual;

            internal BoxedConditionalSubscriber(IConditionalSubscriber<object> actual)
            {
                this.actual = actual;
            }

            public override void OnComplete()
            {
                actual.OnComplete();
            }

            public override void OnError(Exception cause)
            {
                actual.OnError(cause);
            }

            public override void OnNext(T element)
            {
                actual.OnNext(element);
            }

            public override bool Poll(out object item)
            {
                if (qs.Poll(out T v))
                {
                    item = (object)v;
                    return true;
                }
                item = null;
                return false;
            }

            public override bool TryOnNext(T element)
            {
                return actual.TryOnNext(element);
            }

            protected override void OnStart(ISubscription subscription)
            {
                actual.OnSubscribe(this);
            }
        }
    }
}
