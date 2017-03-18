using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Reactive.Streams;
using Reactive4.NET.operators;
using System.Threading;
using Reactive4.NET.utils;

namespace Reactive4.NET
{
    /// <summary>
    /// A Processor that allows only one subscriber and buffers items
    /// until this single subscriber subscribes and after that if the
    /// subscriber can't keep up.
    /// </summary>
    /// <typeparam name="T">The input and output value type.</typeparam>
    public sealed class UnicastProcessor<T> : IFlowableProcessor<T>, IDisposable
    {
        /// <summary>
        /// Indicates that this IFlowableProcessor has completed normally.
        /// </summary>
        public bool HasComplete => Volatile.Read(ref done) && error == null;

        /// <summary>
        /// Indicates that this IFlowableProcessor has terminated with an exception.
        /// </summary>
        public bool HasException => Volatile.Read(ref done) && error != null;

        /// <summary>
        /// Returns the terminal exception if HasException is true, null otherwise.
        /// </summary>
        public Exception Exception => Volatile.Read(ref done) ? error : null;

        /// <summary>
        /// Indicates there are any subscribers subscribed to this IFlowableProcessor.
        /// </summary>
        public bool HasSubscribers => Volatile.Read(ref actual) != null;

        readonly int bufferSize;

        readonly ISimpleQueue<T> queue;

        IFlowableSubscriber<T> actual;

        ISubscription upstream;

        Action onTerminate;

        int once;

        bool outputFused;
        bool cancelled;
        bool done;
        Exception error;

        int wip;

        long requested;

        long emitted;

        /// <summary>
        /// Constructs an UnicastProcessor with a default buffer size and
        /// no termination action.
        /// </summary>
        public UnicastProcessor() : this(Flowable.BufferSize(), () => { })
        {

        }

        /// <summary>
        /// Constructs an UnicastProcessor with the given buffer size
        /// and no termination action.
        /// </summary>
        /// <param name="bufferSize">The island size of the internal unbounded buffer.</param>
        public UnicastProcessor(int bufferSize) : this(bufferSize, () => { })
        {

        }

        /// <summary>
        /// Constructs an UnicastProcessor with the given buffer size
        /// and termination action (called at most once on a terminal event
        /// or when the single subscriber cancels).
        /// </summary>
        /// <param name="bufferSize">The island size of the internal unbounded buffer.</param>
        /// <param name="onTerminate">The action called when this UnicastProcessor
        /// receives a terminal event or cancellation.</param>
        public UnicastProcessor(int bufferSize, Action onTerminate)
        {
            this.bufferSize = bufferSize;
            this.queue = new SpscLinkedArrayQueue<T>(bufferSize);
            Volatile.Write(ref this.onTerminate, onTerminate);
        }

        void Terminate()
        {
            Action a = Interlocked.Exchange(ref onTerminate, null);
            a?.Invoke();
        }

        /// <summary>
        /// Successful terminal state.
        /// No further events will be sent even if Reactive.Streams.ISubscription.Request(System.Int64)
        /// is invoked again.
        /// </summary>
        public void OnComplete()
        {
            if (Volatile.Read(ref done) || Volatile.Read(ref cancelled))
            {
                return;
            }
            Terminate();
            Volatile.Write(ref done, true);
            Drain();
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
            if (Volatile.Read(ref done) || Volatile.Read(ref cancelled))
            {
                return;
            }
            error = cause;
            Terminate();
            Volatile.Write(ref done, true);
            Drain();
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
            if (Volatile.Read(ref done) || Volatile.Read(ref cancelled))
            {
                return;
            }
            queue.Offer(element);
            Drain();
        }

        /// <summary>
        /// Cancels the upstream ISubscription.
        /// </summary>
        public void Dispose()
        {
            SubscriptionHelper.Cancel(ref upstream);
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
            if (SubscriptionHelper.SetOnce(ref upstream, subscription, false))
            {
                if (Volatile.Read(ref done) || Volatile.Read(ref cancelled))
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

        void Cancel()
        {
            if (Volatile.Read(ref cancelled))
            {
                return;
            }
            Volatile.Write(ref cancelled, true);
            Volatile.Write(ref actual, null);
            Terminate();
            SubscriptionHelper.Cancel(ref upstream);
            if (Interlocked.Increment(ref wip) == 1)
            {
                queue.Clear();
            }
        }

        void Request(long n)
        {
            if (n <= 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(n));
            }
            SubscriptionHelper.AddRequest(ref requested, n);
            Drain();
        }

        void Drain()
        {
            if (Interlocked.Increment(ref wip) != 1)
            {
                return;
            }
            if (outputFused)
            {
                DrainFused();
            }
            else
            {
                DrainAsync();
            }
        }

        void DrainAsync()
        {
            var q = queue;
            var a = Volatile.Read(ref actual);
            int missed = 1;
            long e = emitted;

            for (;;)
            {
                if (a != null)
                {
                    long r = Volatile.Read(ref requested);

                    while (e != r)
                    {
                        if (Volatile.Read(ref cancelled))
                        {
                            q.Clear();
                            return;
                        }

                        bool d = Volatile.Read(ref done);
                        bool empty = !q.Poll(out T v);

                        if (d && empty)
                        {
                            Volatile.Write(ref actual, null);
                            Exception ex = error;
                            if (ex != null)
                            {
                                a.OnError(ex);
                            }
                            else
                            {
                                a.OnComplete();
                            }
                            return;
                        }

                        if (empty)
                        {
                            break;
                        }

                        a.OnNext(v);

                        e++;
                    }

                    if (e == r)
                    {
                        if (Volatile.Read(ref done) && q.IsEmpty())
                        {
                            Volatile.Write(ref actual, null);
                            Exception ex = error;
                            if (ex != null)
                            {
                                a.OnError(ex);
                            }
                            else
                            {
                                a.OnComplete();
                            }
                            return;
                        }
                    }
                }
                int w = Volatile.Read(ref wip);
                if (w == missed)
                {
                    emitted = e;
                    missed = Interlocked.Add(ref wip, -missed);
                    if (missed == 0)
                    {
                        break;
                    }
                }
                else
                {
                    missed = w;
                }
                if (a == null)
                {
                    a = Volatile.Read(ref actual);
                }
            }
        }

        void DrainFused()
        {
            var q = queue;
            var a = Volatile.Read(ref actual);
            int missed = 1;

            for (;;)
            {
                if (a != null)
                {
                    if (Volatile.Read(ref cancelled))
                    {
                        q.Clear();
                        return;
                    }
                    bool d = Volatile.Read(ref done);

                    a.OnNext(default(T));

                    if (d)
                    {
                        Volatile.Write(ref actual, null);
                        Exception ex = error;
                        if (ex != null)
                        {
                            a.OnError(ex);
                        }
                        else
                        {
                            a.OnComplete();
                        }
                        return;
                    }
                }
                int w = Volatile.Read(ref wip);
                if (w == missed)
                {
                    missed = Interlocked.Add(ref wip, -missed);
                    if (missed == 0)
                    {
                        break;
                    }
                }
                else
                {
                    missed = w;
                }
                if (a == null)
                {
                    a = Volatile.Read(ref actual);
                }
            }
        }

        /// <summary>
        /// Subscribe with the relaxed IFlowableSubscriber instance.
        /// </summary>
        /// <param name="subscriber">The IFlowableSubscriber instance, not null.</param>
        public void Subscribe(IFlowableSubscriber<T> subscriber)
        {
            if (Interlocked.CompareExchange(ref once, 1, 0) == 0)
            {
                subscriber.OnSubscribe(new UnicastSubscription(this));
                Volatile.Write(ref actual, subscriber);
                if (Volatile.Read(ref cancelled))
                {
                    Volatile.Write(ref actual, null);
                }
                else
                {
                    Drain();
                }
            }
            else
            {
                subscriber.OnSubscribe(EmptySubscription<T>.Instance);
                subscriber.OnError(new InvalidOperationException("UnicastProcessor supports only one ISubscriber"));
            }
        }

        sealed class UnicastSubscription : IQueueSubscription<T>
        {
            readonly UnicastProcessor<T> parent;

            internal UnicastSubscription(UnicastProcessor<T> parent)
            {
                this.parent = parent;
            }

            public void Cancel()
            {
                parent.Cancel();
            }

            public void Clear()
            {
                parent.queue.Clear();
            }

            public bool IsEmpty()
            {
                return parent.queue.IsEmpty();
            }

            public bool Offer(T item)
            {
                throw new InvalidOperationException("Should not be called!");
            }

            public bool Poll(out T item)
            {
                return parent.queue.Poll(out item);
            }

            public void Request(long n)
            {
                parent.Request(n);
            }

            public int RequestFusion(int mode)
            {
                if ((mode & FusionSupport.ASYNC) != 0)
                {
                    parent.outputFused = true;
                    return FusionSupport.ASYNC;
                }
                return FusionSupport.NONE;
            }
        }
    }
}
