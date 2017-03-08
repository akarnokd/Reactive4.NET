using Reactive4.NET.operators;
using System;
using System.Collections.Generic;
using System.Threading;
using Reactive.Streams;
using System.Text;
using System.Collections;

namespace Reactive4.NET
{
    public class TestSubscriber<T> : IFlowableSubscriber<T>, IDisposable, ISubscription
    {
        readonly IList<T> values;

        readonly IList<Exception> errors;

        readonly CountdownEvent latch;

        long completions;

        long requested;

        long valueCount;

        long errorCount;

        ISubscription upstream;

        public TestSubscriber(long initialRequest = 0)
        {
            this.requested = initialRequest;
            this.values = new List<T>();
            this.errors = new List<Exception>();
            this.latch = new CountdownEvent(1);
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
            Volatile.Write(ref completions, completions + 1);
            latch.Signal();
        }

        public virtual void OnError(Exception e)
        {
            errors.Add(e);
            Volatile.Write(ref errorCount, errorCount + 1);
            latch.Signal();
        }

        public virtual void OnNext(T t)
        {
            values.Add(t);
            Volatile.Write(ref valueCount, valueCount + 1);
        }

        public void OnSubscribe(ISubscription subscription)
        {
            SubscriptionHelper.DeferredSetOnce(ref upstream, ref requested, subscription);
        }

        public void Request(long n)
        {
            SubscriptionHelper.DeferredRequest(ref upstream, ref requested, n);
        }

        protected InvalidOperationException fail(string message)
        {
            StringBuilder b = new StringBuilder(64);

            b.Append(message);
            b.Append(" (latch = ").Append(latch.CurrentCount);
            b.Append(", values = ").Append(Volatile.Read(ref valueCount));
            long ec = Volatile.Read(ref errorCount);
            b.Append(", errors = ").Append(ec);
            b.Append(", completions = ").Append(Volatile.Read(ref completions));
            b.Append(", subscribed = ").Append(Volatile.Read(ref upstream) != null);
            b.Append(", cancelled = ").Append(SubscriptionHelper.IsCancelled(ref upstream));
            b.Append(")");

            InvalidOperationException ex;

            if (ec != 0)
            {
                if (ec == 1)
                {
                    ex = new InvalidOperationException(b.ToString(), errors[0]);
                }
                else
                {
                    ex = new InvalidOperationException(b.ToString(), new AggregateException(errors));
                }
            }
            else
            {
                ex = new InvalidOperationException(b.ToString());
            }
            return ex;
        }

        public TestSubscriber<T> AwaitDone(TimeSpan timespan)
        {
            if (latch.CurrentCount != 0L)
            {
                try
                {
                    if (!latch.Wait(timespan))
                    {
                        Cancel();
                        throw fail("Timeout!");
                    }
                } catch (ObjectDisposedException)
                {
                    Cancel();
                    throw;
                }
            }
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

        public TestSubscriber<T> AssertSubscribed()
        {
            if (Volatile.Read(ref upstream) == null)
            {
                throw fail("OnSubscribe not called");
            }
            return this;
        }

        public TestSubscriber<T> AssertValues(params T[] expected)
        {
            long vc = Volatile.Read(ref valueCount);
            if (expected.Length != vc)
            {
                throw fail("Number of values differ. Expected: " + expected.Length + ", Actual: " + vc);
            }

            IEqualityComparer<T> comparer = EqualityComparer<T>.Default;
            for (int i = 0; i < vc; i++)
            {
                if (!comparer.Equals(expected[i], values[i]))
                {
                    throw fail("Value at " + i + " differs. Expected: " + AsString(expected[i]) + ", Actual: " + AsString(values[i]));
                }
            }
            return this;
        }

        public TestSubscriber<T> AssertResult(params T[] expected)
        {
            AssertSubscribed();
            if (Volatile.Read(ref errorCount) != 0)
            {
                throw fail("Error(s) present");
            }

            if (Volatile.Read(ref completions) != 1)
            {
                throw fail("Not (properly) completed");
            }

            AssertValues(expected);
            return this;
        }

        string AsString(object item)
        {
            if (item is IList lst)
            {
                StringBuilder b = new StringBuilder();
                b.Append("[");
                for (int i = 0; i < lst.Count; i++)
                {
                    if (i != 0)
                    {
                        b.Append(", ");
                    }
                    b.Append(AsString(lst[i]));
                }
                b.Append("]");
            }

            return item == null ? item.ToString() : "null";
        }

        public TestSubscriber<T> AssertFailure(Type exception, params T[] expected)
        {
            AssertSubscribed();
            long ec = Volatile.Read(ref errorCount);
            if (ec == 0)
            {
                throw fail("No errors present");
            }
            if (ec == 1)
            {
                if (!exception.IsInstanceOfType(errors[0]))
                {
                    throw fail("Different error present");
                }
            } else
            {
                for (int i = 0; i < ec; i++)
                {
                    if (!exception.IsInstanceOfType(errors[i]))
                    {
                        throw fail("Error found but there are others");
                    }
                }
                throw fail("Different errors present");
            }

            if (Volatile.Read(ref completions) != 0)
            {
                throw fail("Completed");
            }

            AssertValues(expected);
            return this;
        }
    }
}
