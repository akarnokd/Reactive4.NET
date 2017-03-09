using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Reactive.Streams;
using Reactive4.NET.utils;
using System.Threading;

namespace Reactive4.NET.operators
{
    sealed class FlowableReduce<T, R> : AbstractFlowableOperator<T, R>
    {
        readonly Func<R> initialSupplier;

        readonly Func<R, T, R> reducer;

        public FlowableReduce(IFlowable<T> source, Func<R> initialSupplier, Func<R, T, R> reducer) : base(source)
        {
            this.initialSupplier = initialSupplier;
            this.reducer = reducer;
        }

        public override void Subscribe(IFlowableSubscriber<R> subscriber)
        {
            R initial;

            try
            {
                initial = initialSupplier();
                if (initial == null)
                {
                    throw new NullReferenceException("The initialSupplier returned a null value");
                }
            }
            catch (Exception ex)
            {
                subscriber.OnSubscribe(EmptySubscription<T>.Instance);
                subscriber.OnError(ex);
                return;
            }

            source.Subscribe(new ReduceSubscriber(subscriber, initial, reducer));
        }

        sealed class ReduceSubscriber : AbstractDeferredScalarSubscription<R>, IFlowableSubscriber<T>
        {
            readonly Func<R, T, R> reducer;

            ISubscription upstream;

            public ReduceSubscriber(IFlowableSubscriber<R> actual, R initial, Func<R, T, R> reducer) : base(actual)
            {
                this.value = initial;
                this.reducer = reducer;
            }

            public void OnComplete()
            {
                Complete(value);
            }

            public void OnError(Exception cause)
            {
                if (Volatile.Read(ref state) != STATE_CANCELLED)
                {
                    value = default(R);
                    Volatile.Write(ref state, STATE_CANCELLED);
                    actual.OnError(cause);
                }
            }

            public void OnNext(T element)
            {
                try
                {
                    R v = reducer(value, element);
                    if (v == null)
                    {
                        throw new NullReferenceException("The reducer returned a null value");
                    }
                    value = v;
                }
                catch (Exception ex)
                {
                    upstream.Cancel();
                    OnError(ex);
                }
            }

            public void OnSubscribe(ISubscription subscription)
            {
                if (SubscriptionHelper.Validate(ref upstream, subscription))
                {
                    actual.OnSubscribe(this);

                    subscription.Request(long.MaxValue);
                }
            }
        }
    }
}
