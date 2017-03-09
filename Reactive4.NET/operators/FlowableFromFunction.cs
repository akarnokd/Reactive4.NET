using Reactive4.NET.utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reactive4.NET.operators
{
    sealed class FlowableFromFunction<T> : AbstractFlowableSource<T>, IVariableSource<T>
    {
        readonly Func<T> function;

        internal FlowableFromFunction(Func<T> function)
        {
            this.function = function;
        }

        public override void Subscribe(IFlowableSubscriber<T> subscriber)
        {
            var s = new FromFunctionSubscription(subscriber);
            subscriber.OnSubscribe(s);
            T item;
            try
            {
                item = function();
                if (item == null)
                {
                    throw new NullReferenceException("The function returned a null value");
                }
            }
            catch (Exception ex)
            {
                if (!s.IsCancelled())
                {
                    subscriber.OnError(ex);
                }
                return;
            }
            s.Complete(item);
        }

        public bool Value(out T value)
        {
            value = function();
            return true;
        }

        sealed class FromFunctionSubscription : AbstractDeferredScalarSubscription<T>
        {
            public FromFunctionSubscription(IFlowableSubscriber<T> actual) : base(actual)
            {
            }
        }
    }
}
