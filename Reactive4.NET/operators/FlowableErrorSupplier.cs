using System;
using System.Collections.Generic;
using System.Text;

namespace Reactive4.NET.operators
{
    internal sealed class FlowableErrorSupplier<T> : AbstractFlowableSource<T>, IConstantSource<T>
    {
        readonly Func<Exception> errorSupplier;

        internal FlowableErrorSupplier(Func<Exception> errorSupplier)
        {
            this.errorSupplier = errorSupplier;
        }

        public override void Subscribe(IFlowableSubscriber<T> subscriber)
        {
            subscriber.OnSubscribe(EmptySubscription<T>.Instance);

            Exception error;

            try
            {
                error = errorSupplier();
                if (error == null)
                {
                    error = new NullReferenceException("The errorSupplier returned an null Exception");
                }
            }
            catch (Exception ex)
            {
                error = ex;
            }

            subscriber.OnError(error);
        }

        public bool Value(out T value)
        {
            throw errorSupplier();
        }
    }
}
