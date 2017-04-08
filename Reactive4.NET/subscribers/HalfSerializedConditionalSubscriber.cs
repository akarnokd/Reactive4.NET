using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Reactive.Streams;
using Reactive4.NET.utils;
using Reactive4.NET.operators;

namespace Reactive4.NET.subscribers
{
    /// <summary>
    /// A ISubscriber wrapper that allows one thread to signal OnNext which is
    /// serialized in respect to any other threads signalling OnError or OnComplete.
    /// </summary>
    /// <typeparam name="T">The value type</typeparam>
    sealed class HalfSerializedConditionalSubscriber<T> : IConditionalSubscriber<T>, ISubscription
    {
        readonly IConditionalSubscriber<T> actual;

        int wip;

        Exception error;

        ISubscription upstream;

        internal HalfSerializedConditionalSubscriber(IConditionalSubscriber<T> actual)
        {
            this.actual = actual;
        }

        public void Cancel()
        {
            upstream.Cancel();
        }

        public void OnComplete()
        {
            SerializationHelper.OnComplete(actual, ref wip, ref error);
        }

        public void OnError(Exception cause)
        {
            SerializationHelper.OnError(actual, ref wip, ref error, cause);
        }

        public void OnNext(T element)
        {
            SerializationHelper.OnNext(actual, ref wip, ref error, element);
        }

        public bool TryOnNext(T element)
        {
            return SerializationHelper.TryOnNext(actual, ref wip, ref error, element);
        }

        public void OnSubscribe(ISubscription subscription)
        {
            if (SubscriptionHelper.Validate(ref upstream, subscription))
            {
                actual.OnSubscribe(this);
            }
        }

        public void Request(long n)
        {
            upstream.Request(n);
        }
    }
}
