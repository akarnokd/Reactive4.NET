using Reactive4.NET.operators;
using System;
using System.Collections.Generic;
using System.Threading;
using Reactive.Streams;

namespace Reactive4.NET
{
    public class TestSubscriber<T> : IFlowableSubscriber<T>, IDisposable, ISubscription
    {
        readonly IList<T> values;

        readonly IList<Exception> errors;

        long completions;

        bool hasSubscribed;

        long requested;

        long valueCount;

        long errorCount;

        ISubscription upstream;

        public TestSubscriber(long initialRequest = 0)
        {
            this.requested = initialRequest;
            this.values = new List<T>();
            this.errors = new List<Exception>();
        }

        public void Cancel()
        {
            SubscriptionHelper.Cancel(ref upstream);
        }

        public void Dispose()
        {
            Cancel();
        }

        public virtual void OnComplete()
        {
            completions++;
        }

        public virtual void OnError(Exception e)
        {
            errors.Add(e);
            Volatile.Write(ref errorCount, errorCount + 1);
        }

        public virtual void OnNext(T t)
        {
            values.Add(t);
            Volatile.Write(ref valueCount, valueCount + 1);
        }

        public void OnSubscribe(ISubscription subscription)
        {
            throw new NotImplementedException();
        }

        public void Request(long n)
        {
            throw new NotImplementedException();
        }

        public TestSubscriber<T> AwaitDone(TimeSpan timespan)
        {
            // TODO implement
            return this;
        }

        public TestSubscriber<T> AwaitCount(long count, Action waitStrategy, TimeSpan timeout)
        {
            // TODO implement
            return this;
        }

        public TestSubscriber<T> RequestMore(long n)
        {
            Request(n);
            return this;
        }

        public TestSubscriber<T> AssertResult(params T[] expected)
        {
            // TODO implement
            return this;
        }

        public TestSubscriber<T> AssertFailure(Type exception, params T[] expected)
        {
            // TODO implement
            return this;
        }
    }
}
