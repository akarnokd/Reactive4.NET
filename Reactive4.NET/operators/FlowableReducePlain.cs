using Reactive4.NET.utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Reactive.Streams;
using System.Threading;

namespace Reactive4.NET.operators
{
    sealed class FlowableReducePlain<T> : AbstractFlowableOperator<T, T>
    {
        readonly Func<T, T, T> reducer;

        public FlowableReducePlain(IFlowable<T> source, Func<T, T, T> reducer) : base(source)
        {
            this.reducer = reducer;
        }

        public override void Subscribe(IFlowableSubscriber<T> subscriber)
        {
            source.Subscribe(new ReducePlainSubscriber(subscriber, reducer));
        }

        internal sealed class ReducePlainSubscriber : AbstractDeferredScalarSubscription<T>, IFlowableSubscriber<T>
        {
            readonly Func<T, T, T> reducer;

            bool hasValue;

            ISubscription upstream;

            public ReducePlainSubscriber(IFlowableSubscriber<T> actual, Func<T, T, T> reducer) : base(actual)
            {
                this.reducer = reducer;
            }

            public void OnComplete()
            {
                if (hasValue)
                {
                    Complete(value);
                }
                else
                {
                    Complete();
                }
            }

            public void OnError(Exception cause)
            {
                if (Volatile.Read(ref state) != STATE_CANCELLED)
                {
                    value = default(T);
                    Volatile.Write(ref state, STATE_CANCELLED);
                    actual.OnError(cause);
                }
            }

            public void OnNext(T element)
            {
                if (hasValue)
                {
                    try
                    {
                        T v = reducer(value, element);
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
                        return;
                    }
                }
                else
                {
                    hasValue = true;
                    value = element;
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
