using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Reactive.Streams;
using Reactive4.NET.operators;
using System.Threading;

namespace Reactive4.NET
{
    /// <summary>
    /// Multicasts events to subscribers; if a subscriber can't keep up
    /// it will receive an InvalidOperationException.
    /// </summary>
    /// <typeparam name="T">The input and output value type.</typeparam>
    public sealed class DirectProcessor<T> : IFlowableProcessor<T>, IDisposable
    {
        /// <summary>
        /// Indicates that this IFlowableProcessor has completed normally.
        /// </summary>
        public bool HasComplete {
            get
            {
                return Volatile.Read(ref subscribers) == Terminated && error == null;
            }
        }

        /// <summary>
        /// Indicates that this IFlowableProcessor has terminated with an exception.
        /// </summary>
        public bool HasException
        {
            get
            {
                return Volatile.Read(ref subscribers) == Terminated && error != null;
            }
        }

        /// <summary>
        /// Returns the terminal exception if HasException is true, null otherwise.
        /// </summary>
        public Exception Exception
        {
            get
            {
                return Volatile.Read(ref subscribers) == Terminated ? error : null;
            }
        }

        /// <summary>
        /// Indicates there are any subscribers subscribed to this IFlowableProcessor.
        /// </summary>
        public bool HasSubscribers
        {
            get
            {
                return Volatile.Read(ref subscribers).Length != 0;
            }
        }

        ProcessorSubscription[] subscribers = Empty;

        static readonly ProcessorSubscription[] Empty = new ProcessorSubscription[0];
        static readonly ProcessorSubscription[] Terminated = new ProcessorSubscription[0];

        Exception error;
        int once;

        ISubscription upstream;

        /// <summary>
        /// Cancels the upstream ISubscription.
        /// </summary>
        public void Dispose()
        {
            SubscriptionHelper.Cancel(ref upstream);
        }

        /// <summary>
        /// Returns true if the upstream ISubscription has been disposed.
        /// </summary>
        public bool IsDisposed => SubscriptionHelper.IsCancelled(ref upstream);

        /// <summary>
        /// Successful terminal state.
        /// No further events will be sent even if Reactive.Streams.ISubscription.Request(System.Int64)
        /// is invoked again.
        /// </summary>
        public void OnComplete()
        {
            if (Interlocked.CompareExchange(ref once, 1, 0) == 0)
            {
                foreach (var inner in Interlocked.Exchange(ref subscribers, Terminated))
                {
                    inner.OnComplete();
                }
            }
        }

        /// <summary>
        /// Failed terminal state.
        /// No further events will be sent even if Reactive.Streams.ISubscription.Request(System.Int64)
        /// is invoked again.
        /// </summary>
        /// <param name="cause">The exception signaled.</param>
        public void OnError(Exception cause)
        {
            if (cause == null)
            {
                throw new ArgumentNullException(nameof(cause));
            }
            if (Interlocked.CompareExchange(ref once, 1, 0) == 0)
            {
                error = cause;
                foreach (var inner in Interlocked.Exchange(ref subscribers, Terminated))
                {
                    inner.OnComplete();
                }
            }
        }

        /// <summary>
        /// Data notification sent by the IPublisher in response to requests
        /// to ISubscription.Request(long).
        /// </summary>
        /// <param name="element">The element signaled</param>
        public void OnNext(T element)
        {
            if (element == null)
            {
                throw new ArgumentNullException(nameof(element));
            }
            foreach (var inner in Volatile.Read(ref subscribers))
            {
                inner.OnNext(element);
            }
        }

        /// <summary>
        /// Tries to multicast an item if all of the current subscribers
        /// are ready to receive. Should be called in a sequential manner,
        /// just like the other OnXXX methods.
        /// </summary>
        /// <param name="element">The item to multicast</param>
        /// <returns>True if the item was multicast successfully, false if
        /// one or more subscribers are not ready to receive items.</returns>
        public bool Offer(T element)
        {
            if (element == null)
            {
                throw new ArgumentNullException(nameof(element));
            }
            var a = Volatile.Read(ref subscribers);
            foreach (var inner in a)
            {
                if (inner.IsFull())
                {
                    return false;
                }
            }
            foreach (var inner in a)
            {
                inner.OnNext(element);
            }
            return true;
        }

        /// <summary>
        /// Invoked after calling IPublisher.Subscribe(ISubscriber).
        /// No data will start flowing until ISubscription.Request(long)
        /// is invoked.
        /// It is the responsibility of this ISubscriber instance to call
        /// Reactive.Streams.ISubscription.Request(System.Int64) whenever more data is wanted.
        /// The IPublisher will send notifications only in response to
        /// Reactive.Streams.ISubscription.Request(System.Int64).
        /// </summary>
        /// <param name="subscription">ISubscription that allows requesting data via ISubscription.Request(long)</param>
        public void OnSubscribe(ISubscription subscription)
        {
            if (SubscriptionHelper.SetOnce(ref upstream, subscription, crash: false))
            {
                if (Volatile.Read(ref subscribers) == Terminated)
                {
                    subscription.Cancel();
                }
                else
                {
                    subscription.Request(long.MaxValue);
                }
            }
        }

        /// <summary>
        /// Request IPublisher to start streaming data.
        /// This is a "factory method" and can be called multiple times, each time starting
        /// a new ISubscription.
        /// Each ISubscription will work for only a single ISubscriber.
        /// A ISubscriber should only subscribe once to a single IPublisher.
        /// If IPublisher rejects the subscription attempt or otherwise
        /// fails it will signal the error via ISubscriber.OnError(Exception).
        /// </summary>
        /// <param name="subscriber">The ISubscriber that will consume signals from this IPublisher</param>
        public void Subscribe(ISubscriber<T> subscriber)
        {
            if (subscriber == null)
            {
                throw new ArgumentNullException(nameof(subscriber));
            }
            if (subscriber is IFlowableSubscriber<T> s)
            {
                Subscribe(s);
            }
            else
            {
                Subscribe(new StrictSubscriber<T>(subscriber));
            }
        }

        bool Add(ProcessorSubscription inner)
        {
            for (;;)
            {
                var a = Volatile.Read(ref subscribers);
                if (a == Terminated)
                {
                    return false;
                }
                int n = a.Length;
                var b = new ProcessorSubscription[n + 1];
                Array.Copy(a, 0, b, 0, n);
                b[n] = inner;
                if (Interlocked.CompareExchange(ref subscribers, b, a) == a)
                {
                    return true;
                }
            }
        }

        void Remove(ProcessorSubscription inner)
        {
            for (;;)
            {
                var a = Volatile.Read(ref subscribers);
                int n = a.Length;
                if (n == 0)
                {
                    break;
                }
                int j = -1;
                for (int i = 0; i < n; i++)
                {
                    if (a[i] == inner)
                    {
                        j = i;
                        break;
                    }
                }
                if (j < 0)
                {
                    break;
                }
                ProcessorSubscription[] b;
                if (n == 1)
                {
                    b = Empty;
                }
                else
                {
                    b = new ProcessorSubscription[n - 1];
                    Array.Copy(a, 0, b, 0, j);
                    Array.Copy(a, j + 1, b, j, n - j - 1);
                }
                if (Interlocked.CompareExchange(ref subscribers, b, a) == a)
                {
                    break;
                }
            }
        }

        /// <summary>
        /// Subscribe with the relaxed IFlowableSubscriber instance.
        /// </summary>
        /// <param name="subscriber">The IFlowableSubscriber instance, not null.</param>
        public void Subscribe(IFlowableSubscriber<T> subscriber)
        {
            ProcessorSubscription ps = new ProcessorSubscription(subscriber, this);
            subscriber.OnSubscribe(ps);
            if (Add(ps))
            {
                if (ps.IsCancelled())
                {
                    Remove(ps);
                    return;
                }
            }
            else
            {
                Exception ex = error;
                if (ex != null)
                {
                    ps.OnError(ex);
                }
                else
                {
                    ps.OnComplete();
                }
            }
        }

        sealed class ProcessorSubscription : ISubscription
        {
            readonly IFlowableSubscriber<T> actual;

            readonly DirectProcessor<T> parent;

            long requested;

            long emitted;

            internal ProcessorSubscription(IFlowableSubscriber<T> actual, DirectProcessor<T> parent)
            {
                this.actual = actual;
                this.parent = parent;
            }

            internal bool IsFull()
            {
                return Volatile.Read(ref requested) == emitted;
            }

            public void Cancel()
            {
                if (Interlocked.Exchange(ref requested, long.MinValue) != long.MinValue)
                {
                    parent.Remove(this);
                }
            }

            public void Request(long n)
            {
                if (SubscriptionHelper.Validate(n))
                {
                    for (;;)
                    {
                        long r = Volatile.Read(ref requested);
                        if (r == long.MinValue)
                        {
                            break;
                        }
                        long u = r + n;
                        if (u < 0L)
                        {
                            u = long.MaxValue;
                        }
                        if (Interlocked.CompareExchange(ref requested, u, r) == r)
                        {
                            break;
                        }
                    }
                }
            }

            internal void OnNext(T element)
            {
                var r = Volatile.Read(ref requested);
                if (r != long.MinValue)
                {
                    long e = emitted;
                    if (r != e)
                    {
                        actual.OnNext(element);
                        emitted = e + 1;
                    }
                    else
                    {
                        if (Interlocked.Exchange(ref requested, long.MinValue) != long.MinValue)
                        {
                            parent.Remove(this);
                            actual.OnError(new InvalidOperationException("Could not emit value due to lack of requests"));
                        }
                    }
                }
            }

            internal void OnError(Exception cause)
            {
                if (Volatile.Read(ref requested) != long.MinValue)
                {
                    actual.OnError(cause);
                }
            }

            internal void OnComplete()
            {
                if (Volatile.Read(ref requested) != long.MinValue)
                {
                    actual.OnComplete();
                }
            }

            internal bool IsCancelled()
            {
                return Volatile.Read(ref requested) == long.MinValue;
            }
        }
    }
}
