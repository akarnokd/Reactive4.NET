using Reactive4.NET.operators;
using System;
using System.Collections.Generic;
using System.Threading;
using Reactive.Streams;
using System.Text;
using System.Collections;
using Reactive4.NET.utils;

namespace Reactive4.NET
{
    /// <summary>
    /// An IFlowableSubsriber that offers Assertions about the state of the sequence.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
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

        bool hasSubscribed;

        bool checkSubscribed;

        string tag;

        /// <summary>
        /// Constructs a new TestSubscriber instance with an optional initial request.
        /// </summary>
        /// <param name="initialRequest">The initial request value, non-negative, defaults to long.MaxValue</param>
        public TestSubscriber(long initialRequest = long.MaxValue)
        {
            if (initialRequest < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(initialRequest), "Non-negative value required");
            }
            this.requested = initialRequest;
            this.values = new List<T>();
            this.errors = new List<Exception>();
            this.latch = new CountdownEvent(1);
        }

        /// <summary>
        /// Cancels the upstream (can be called at any time).
        /// </summary>
        public void Cancel()
        {
            SubscriptionHelper.Cancel(ref upstream);
        }

        /// <summary>
        /// Cancels the upstream (can be called at any time).
        /// </summary>
        public void Dispose()
        {
            Cancel();
        }

        /// <summary>
        /// Completes the TestSubscriber.
        /// </summary>
        public virtual void OnComplete()
        {
            CheckSubscribed();
            Volatile.Write(ref completions, completions + 1);
            latch.Signal();
        }

        /// <summary>
        /// Terminates the TestSubscriber with the given error.
        /// </summary>
        /// <param name="e">The error signalled by the upstream.</param>
        public virtual void OnError(Exception e)
        {
            CheckSubscribed();
            errors.Add(e);
            Volatile.Write(ref errorCount, errorCount + 1);
            latch.Signal();
        }

        /// <summary>
        /// Consumes an upstream item.
        /// </summary>
        /// <param name="t">The item consumed.</param>
        public virtual void OnNext(T t)
        {
            CheckSubscribed();
            values.Add(t);
            Volatile.Write(ref valueCount, valueCount + 1);
        }

        /// <summary>
        /// Checks once if the OnSubscribe() method has been called by the upstream.
        /// </summary>
        protected void CheckSubscribed()
        {
            if (!checkSubscribed)
            {
                checkSubscribed = true;
                if (!hasSubscribed)
                {
                    errors.Add(new InvalidOperationException("OnSubscribe not called!"));
                    Volatile.Write(ref errorCount, errorCount + 1);
                }
            }

        }

        /// <summary>
        /// Called when this TestSubscriber gets subscribed to a source
        /// with the ISubscription representing the connection between the two.
        /// </summary>
        /// <param name="subscription">The ISubscription from the upstream.</param>
        public void OnSubscribe(ISubscription subscription)
        {
            hasSubscribed = true;
            SubscriptionHelper.DeferredSetOnce(ref upstream, ref requested, subscription);
        }

        /// <summary>
        /// Requests the specified amount from upstream (can be called at any time).
        /// </summary>
        /// <param name="n">The number to request, positive</param>
        public void Request(long n)
        {
            SubscriptionHelper.DeferredRequest(ref upstream, ref requested, n);
        }

        /// <summary>
        /// Returns an InvalidOperationException with the message and the current
        /// TestSubscriber state.
        /// </summary>
        /// <param name="message">The message to print</param>
        /// <returns>The Exception that can be thrown.</returns>
        public InvalidOperationException Fail(string message)
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
            if (tag != null)
            {
                b.Append(", tag = ").Append(tag);
            }
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

        /// <summary>
        /// Sets a tag with this TestSubscriber to be printed along with
        /// the state if an assertion fails.
        /// </summary>
        /// <param name="tag">The tag to print, null clears the tag and won't print anything.</param>
        /// <returns>this</returns>
        public TestSubscriber<T> WithTag(string tag)
        {
            this.tag = tag;
            return this;
        }

        /// <summary>
        /// The current tag, may be null.
        /// </summary>
        public string Tag => tag;

        /// <summary>
        /// Cancels the upstream in a fluent manner.
        /// </summary>
        /// <returns>this</returns>
        public TestSubscriber<T> ThenCancel()
        {
            Cancel();
            return this;
        }

        /// <summary>
        /// Waits up to the specified timespan for the upstream to
        /// singal a terminal event, otherwise the sequence is cancelled
        /// and a timeout error is thrown
        /// </summary>
        /// <param name="timespan">The time to wait for the upstream to terminate.</param>
        /// <returns>this</returns>
        public TestSubscriber<T> AwaitDone(TimeSpan timespan)
        {
            if (latch.CurrentCount != 0L)
            {
                try
                {
                    if (!latch.Wait(timespan))
                    {
                        Cancel();
                        throw Fail("Timeout!");
                    }
                } catch (ObjectDisposedException)
                {
                    Cancel();
                    throw;
                }
            }
            return this;
        }

        /// <summary>
        /// Awaits until the given number of items have been received,
        /// the upstream terminated orthe specified timeout elapses.
        /// </summary>
        /// <param name="count">The number of elements expected.</param>
        /// <param name="waitStrategy">The action called when a wait must happen.</param>
        /// <param name="timeout">The maximum time to wait for the items.</param>
        /// <returns>this</returns>
        public TestSubscriber<T> AwaitCount(long count, Action waitStrategy, TimeSpan timeout)
        {
            long due = SchedulerHelper.NowUTC() + (long)timeout.TotalMilliseconds;
            for (;;)
            {
                if (latch.CurrentCount == 0)
                {
                    return this;
                }
                var n = Volatile.Read(ref valueCount);
                if (n >= count)
                {
                    return this;
                }
                if (due <= SchedulerHelper.NowUTC())
                {
                    return this;
                }

                waitStrategy();
            }
        }

        /// <summary>
        /// Requests the given amount from upstream in a fluent manner.
        /// </summary>
        /// <param name="n">The numer of items requested, positive</param>
        /// <returns>this</returns>
        public TestSubscriber<T> RequestMore(long n)
        {
            Request(n);
            return this;
        }

        /// <summary>
        /// Asserts that the OnSubscribe has been called.
        /// </summary>
        /// <returns>this</returns>
        public TestSubscriber<T> AssertSubscribed()
        {
            if (!hasSubscribed)
            {
                throw Fail("Not subscribed!");
            }
            return this;
        }

        /// <summary>
        /// Asserts that this TestSubscriber has been subscribed but no items
        /// or terminal events have been received.
        /// </summary>
        /// <returns>this</returns>
        public TestSubscriber<T> AssertEmpty()
        {
            AssertSubscribed();
            AssertValues();
            AssertNotTerminated();
            return this;
        }

        /// <summary>
        /// Asserts that this TestSubscriber received exactly one type
        /// of terminal event (OnError or OnComplete).
        /// </summary>
        /// <returns>this</returns>
        public TestSubscriber<T> AssertTerminated()
        {
            if (latch.CurrentCount != 0)
            {
                throw Fail("Not terminated");
            }
            long ec = Volatile.Read(ref errorCount);
            if (ec > 1)
            {
                throw Fail("Terminated with multiple errors");
            }
            long c = Volatile.Read(ref completions);
            if (ec > 1)
            {
                throw Fail("Terminated with multiple completions");
            }
            return this;
        }

        /// <summary>
        /// Asserts that this TestSubscriber did not receive any terminal event.
        /// </summary>
        /// <returns>this</returns>
        public TestSubscriber<T> AssertNotTerminated()
        {
            if (latch.CurrentCount == 0)
            {
                throw Fail("Terminated");
            }
            return this;
        }

        /// <summary>
        /// Assert that the TestSubscriber received the given expected array
        /// of items in that particular order.
        /// </summary>
        /// <param name="expected">The params array of items expected.</param>
        /// <returns>this</returns>
        public TestSubscriber<T> AssertValues(params T[] expected)
        {
            long vc = Volatile.Read(ref valueCount);
            if (expected.Length != vc)
            {
                throw Fail("Number of values differ. Expected: " + expected.Length + ", Actual: " + vc);
            }

            IEqualityComparer<T> comparer = EqualityComparer<T>.Default;
            for (int i = 0; i < vc; i++)
            {
                if (!comparer.Equals(expected[i], values[i]))
                {
                    throw Fail("Value at " + i + " differs. Expected: " + AsString(expected[i]) + ", Actual: " + AsString(values[i]));
                }
            }
            return this;
        }

        /// <summary>
        /// Assert that the TestSubscriber received the given expected array
        /// of items in that particular order.
        /// </summary>
        /// <param name="expected">The params array of items expected.</param>
        /// <returns>this</returns>
        public TestSubscriber<T> AssertValues(IEnumerable<T> expected)
        {
            long vc = Volatile.Read(ref valueCount);

            IEqualityComparer<T> comparer = EqualityComparer<T>.Default;
            using (IEnumerator<T> en = expected.GetEnumerator()) {
                if (en.MoveNext())
                {
                    for (int i = 0; i < vc; i++)
                    {
                        if (!comparer.Equals(en.Current, values[i]))
                        {
                            throw Fail("Value at " + i + " differs. Expected: " + AsString(en.Current) + ", Actual: " + AsString(values[i]));
                        }
                        if (!en.MoveNext() && i < vc - 1)
                        {
                            throw Fail("More values present than " + (i + 1));
                        }
                    }
                }
                else
                {
                    if (vc != 0)
                    {
                        throw Fail("Values present");
                    }
                }
            }
            return this;
        }

        /// <summary>
        /// Asserts that this TestSubscriber has completed normally and
        /// received the given expected items in that particular order.
        /// </summary>
        /// <param name="expected">The params array of expected items.</param>
        /// <returns>this</returns>
        public TestSubscriber<T> AssertResult(params T[] expected)
        {
            AssertSubscribed();
            AssertValues(expected);
            AssertNoError();
            AssertComplete();
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

            return item == null ? "null" : item.ToString();
        }

        /// <summary>
        /// Asserts that this TestSubscriber has terminated exceptionally and
        /// received the given expected items in that particular order before that.
        /// </summary>
        /// <param name="exception">The exception type expected.</param>
        /// <param name="expected">The params array of expected items.</param>
        /// <returns>this</returns>
        public TestSubscriber<T> AssertFailure(Type exception, params T[] expected)
        {
            AssertSubscribed();
            AssertValues(expected);
            AssertError(exception);
            AssertNotComplete();
            return this;
        }

        /// <summary>
        /// Asserts that this TestSubscriber has terminated exceptionally 
        /// with the given message and received the given expected items 
        /// in that particular order before that.
        /// </summary>
        /// <param name="exception">The exception type expected.</param>
        /// <param name="message">The excepted exception message</param>
        /// <param name="expected">The params array of expected items.</param>
        /// <returns>this</returns>
        public TestSubscriber<T> AssertFailureAndMessage(Type exception, string message, params T[] expected)
        {
            AssertSubscribed();
            AssertValues(expected);
            AssertError(exception, message);
            AssertNotComplete();
            return this;
        }

        /// <summary>
        /// Assert that the TestSubscriber received exactly the given number of items.
        /// </summary>
        /// <param name="n">The number of items expected, non-negative</param>
        /// <returns>this</returns>
        public TestSubscriber<T> AssertValueCount(int n)
        {
            long vc = Volatile.Read(ref valueCount);
            if (vc != n)
            {
                throw Fail("Different value count; Expected = " + n + ", Actual = " + vc);
            }
            return this;
        }

        /// <summary>
        /// Assert that the TestSubscriber has received an OnComplete event.
        /// </summary>
        /// <returns>this</returns>
        public TestSubscriber<T> AssertComplete()
        {
            long c = Volatile.Read(ref completions);
            if (c == 0L)
            {
                throw Fail("Not completed");
            }
            if (c > 1L)
            {
                throw Fail("Multiple completions");
            }
            return this;
        }

        /// <summary>
        /// Assert that no completion event has been received.
        /// </summary>
        /// <returns>this</returns>
        public TestSubscriber<T> AssertNotComplete()
        {
            long c = Volatile.Read(ref completions);
            if (c == 1L)
            {
                throw Fail("Completed");
            }
            if (c > 1L)
            {
                throw Fail("Multiple completions");
            }
            return this;
        }

        /// <summary>
        /// Assert that the specified type of error has been received.
        /// </summary>
        /// <param name="errorType">The error type.</param>
        /// <returns>this</returns>
        public TestSubscriber<T> AssertError(Type errorType)
        {
            long ec = Volatile.Read(ref errorCount);
            if (ec == 0)
            {
                throw Fail("No errors present");
            }
            if (ec == 1)
            {
                if (!errorType.IsInstanceOfType(errors[0]))
                {
                    throw Fail("Different error present");
                }
            }
            else
            {
                for (int i = 0; i < ec; i++)
                {
                    if (!errorType.IsInstanceOfType(errors[i]))
                    {
                        throw Fail("Error found but there are others");
                    }
                }
                throw Fail("Different errors present");
            }
            return this;
        }

        /// <summary>
        /// Assert that an error signal with the specified type and message has been received
        /// </summary>
        /// <param name="errorType">The error type expected.</param>
        /// <param name="message">The error message expected</param>
        /// <returns>this</returns>
        public TestSubscriber<T> AssertError(Type errorType, string message)
        {
            AssertError(errorType);
            string m = errors[0].Message;
            if (m != message)
            {
                throw Fail("Different error message; Expected = " + message + ", Actual: " + m);
            }
            return this;
        }

        /// <summary>
        /// Assert that no error has been received.
        /// </summary>
        /// <returns>this</returns>
        public TestSubscriber<T> AssertNoError()
        {
            long ec = Volatile.Read(ref errorCount);
            if (ec != 0L)
            {
                throw Fail("Error(s) present");
            }
            return this;
        }

        /// <summary>
        /// Assert that all received elements pass the predicate.
        /// </summary>
        /// <param name="predicate">The predicate to call with each received element.</param>
        /// <returns>this</returns>
        public TestSubscriber<T> AssertPredicate(Func<T, bool> predicate)
        {
            long vc = Volatile.Read(ref valueCount);
            for (int i = 0; i < vc; i++)
            {
                if (!predicate(values[i]))
                {
                    throw Fail("Item " + i + " failed the predicate");
                }
            }
            return this;
        }

        /// <summary>
        /// Invoke the action fluently with this TestSubscriber.
        /// </summary>
        /// <param name="action">The action to invoke.</param>
        /// <returns>this</returns>
        public TestSubscriber<T> With(Action<TestSubscriber<T>> action)
        {
            action(this);
            return this;
        }

        /// <summary>
        /// The current received value count.
        /// </summary>
        public long ValueCount => Volatile.Read(ref valueCount);

        /// <summary>
        /// The current received error count.
        /// </summary>
        public long ErrorCount => Volatile.Read(ref errorCount);

        /// <summary>
        /// The current received completion count.
        /// </summary>
        public long Completions => Volatile.Read(ref completions);

        /// <summary>
        /// True if a the TestSubscriber has been terminated.
        /// </summary>
        public bool IsTerminated => latch.CurrentCount == 0;

        /// <summary>
        /// The list of received values, safe to read up until ValueCount.
        /// </summary>
        public IList<T> Values => values;

        /// <summary>
        /// The list of received values, safe to read up until ErrorCount.
        /// </summary>
        public IList<Exception> Errors => errors;
    }
}
