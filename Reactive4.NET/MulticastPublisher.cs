using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Reactive.Streams;
using Reactive4.NET.operators;
using Reactive4.NET.utils;
using System.Threading;

namespace Reactive4.NET
{
    /// <summary>
    /// A multicasting publisher that dispatches events to subscribers
    /// on their own IExecutorWorkers.
    /// </summary>
    /// <typeparam name="T">The input and output value type.</typeparam>
    public sealed class MulticastPublisher<T> : IFlowable<T>, IDisposable
    {
        /// <summary>
        /// Indicates that this MulticastPublisher was completed normally.
        /// </summary>
        public bool HasComplete => Volatile.Read(ref error) == ExceptionHelper.Terminated;

        /// <summary>
        /// Indicates that this MulticastPublisher was terminated with an exception.
        /// </summary>
        public bool HasException {
            get
            {
                var ex = Volatile.Read(ref error);
                return ex != null && ex != ExceptionHelper.Terminated;
            }
        }

        /// <summary>
        /// Returns the terminal exception if HasException is true, null otherwise.
        /// </summary>
        public Exception Exception
        {
            get
            {
                var ex = Volatile.Read(ref error);
                return ex != ExceptionHelper.Terminated ? ex : null;
            }
        }

        /// <summary>
        /// Indicates there are any subscribers subscribed to this MulticastPublisher.
        /// </summary>
        public bool HasSubscribers => Volatile.Read(ref subscribers).Length != 0;

        readonly IExecutorService executor;

        readonly int bufferSize;

        Exception error;

        MulticastSubscription[] subscribers;

        static readonly MulticastSubscription[] Empty = new MulticastSubscription[0];

        static readonly MulticastSubscription[] Terminated = new MulticastSubscription[0];

        /// <summary>
        /// Constructs a MulticastPublisher with the Executors.Task service and
        /// default buffer size.
        /// </summary>
        public MulticastPublisher() : this(Executors.Task, Flowable.BufferSize())
        {
        }

        /// <summary>
        /// Constructs a MulticastPublisher with the given executor service
        /// and default buffer size.
        /// </summary>
        /// <param name="executor">The IExecutorService to use.</param>
        public MulticastPublisher(IExecutorService executor) : this(executor, Flowable.BufferSize())
        {
        }

        /// <summary>
        /// Constructs a MulticastPublisher with the given executor service
        /// and given per-subscriber buffer size.
        /// </summary>
        /// <param name="executor">The IExecutorService to use.</param>
        /// <param name="bufferSize">The buffer size per-subscriber, positive.</param>
        public MulticastPublisher(IExecutorService executor, int bufferSize)
        {
            this.executor = executor;
            this.bufferSize = bufferSize;
            this.subscribers = Empty;
        }

        /// <summary>
        /// Subscribe with the relaxed IFlowableSubscriber instance.
        /// </summary>
        /// <param name="subscriber">The IFlowableSubscriber instance, not null.</param>
        public void Subscribe(IFlowableSubscriber<T> subscriber)
        {
            var parent = new MulticastSubscription(this, subscriber, executor.Worker, bufferSize);
            subscriber.OnSubscribe(parent);
            if (Add(parent))
            {
                if (parent.IsCancelled())
                {
                    Remove(parent);
                    return;
                }
            }
            else
            {
                var ex = Volatile.Read(ref error);
                if (ex == ExceptionHelper.Terminated)
                {
                    subscriber.OnComplete();
                }
                else
                {
                    subscriber.OnError(ex);
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

        /// <summary>
        /// Tries to offer an item to all current subscribers or
        /// returns false if all of them have their buffer filled up.
        /// </summary>
        /// <param name="element">The item to offer to all current subscribers.</param>
        /// <returns>True if successful; false if all current subscribers have their
        /// individual buffer filled up.</returns>
        public bool Offer(T element)
        {
            var subs = Volatile.Read(ref subscribers);
            foreach (var ms in subs)
            {
                if (ms.IsFull())
                {
                    return false;
                }
            }
            foreach (var ms in subs)
            {
                ms.OnNext(element);
            }
            return true;
        }

        /// <summary>
        /// Changes the MulticastPublisher into a terminal state and
        /// completes all current and future ISubscribers.
        /// </summary>
        public void Dispose()
        {
            if (Interlocked.CompareExchange(ref error, ExceptionHelper.Terminated, null) == null)
            {
                foreach (var ms in Interlocked.Exchange(ref subscribers, Terminated)) {
                    ms.OnComplete();
                }
            }
        }

        /// <summary>
        /// Changes the MulticastPublisher into a terminal state and
        /// signals an error to all current and future ISubscribers.
        /// </summary>
        /// <param name="ex">The exception to signal, not null.</param>
        public void Fail(Exception ex)
        {
            if (Interlocked.CompareExchange(ref error, ex, null) == null)
            {
                foreach (var ms in Interlocked.Exchange(ref subscribers, Terminated))
                {
                    ms.OnError(ex);
                }
            }
        }

        bool Add(MulticastSubscription ms)
        {
            for (;;)
            {
                var a = Volatile.Read(ref subscribers);
                if (a == Terminated)
                {
                    return false;
                }
                int n = a.Length;
                var b = new MulticastSubscription[n + 1];
                Array.Copy(a, 0, b, 0, n);
                b[n] = ms;
                if (Interlocked.CompareExchange(ref subscribers, b, a) == a)
                {
                    return true;
                }
            }
        }

        void Remove(MulticastSubscription ms)
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
                    if (a[i] == ms)
                    {
                        j = i;
                        break;
                    }
                }

                if (j < 0)
                {
                    break;
                }

                if (n == 1)
                {
                    if (Interlocked.CompareExchange(ref subscribers, Empty, a) == a)
                    {
                        break;
                    }
                }
                else
                {
                    var b = new MulticastSubscription[n - 1];
                    Array.Copy(a, 0, b, 0, j);
                    Array.Copy(a, j + 1, b, j, n - j - 1);
                    if (Interlocked.CompareExchange(ref subscribers, b, a) == a)
                    {
                        break;
                    }
                }
            }
        }

        sealed class MulticastSubscription : IQueueSubscription<T>
        {
            readonly MulticastPublisher<T> parent;

            readonly IFlowableSubscriber<T> actual;

            readonly SpscArrayQueue<T> queue;

            readonly IExecutorWorker worker;

            readonly Action run;

            int wip;
            int cancelled;
            bool done;
            Exception error;

            long requested;

            long emitted;

            bool outputFused;

            internal MulticastSubscription(MulticastPublisher<T> parent, IFlowableSubscriber<T> actual, IExecutorWorker worker, int bufferSize)
            {
                this.parent = parent;
                this.actual = actual;
                this.queue = new SpscArrayQueue<T>(bufferSize);
                this.worker = worker;
                this.run = Run;
            }

            public void Cancel()
            {
                if (Interlocked.CompareExchange(ref cancelled, 1, 0) == 0)
                {
                    parent.Remove(this);
                    worker.Dispose();
                    if (!outputFused && Interlocked.Increment(ref wip) == 1)
                    {
                        queue.Clear();
                    }
                }
            }

            public void Clear()
            {
                queue.Clear();
            }

            public bool IsEmpty()
            {
                return queue.IsEmpty();
            }

            public bool Offer(T item)
            {
                throw new InvalidOperationException("Should not be called!");
            }

            public bool Poll(out T item)
            {
                return queue.Poll(out item);
            }

            public void Request(long n)
            {
                if (SubscriptionHelper.Validate(n))
                {
                    SubscriptionHelper.AddRequest(ref requested, n);
                    Schedule();
                }
            }

            public int RequestFusion(int mode)
            {
                if ((mode & FusionSupport.ASYNC) != 0)
                {
                    outputFused = true;
                    return FusionSupport.ASYNC;
                }
                return FusionSupport.NONE;
            }

            internal void Schedule()
            {
                if (Interlocked.Increment(ref wip) == 1)
                {
                    worker.Schedule(run);
                }
            }

            void Run()
            {
                if (outputFused)
                {
                    DrainFused();
                }
                else
                {
                    DrainNormal();
                }
            }

            void DrainFused()
            {
                int missed = 1;
                var a = actual;
                var q = queue;

                for (;;)
                {
                    bool d = Volatile.Read(ref done);

                    if (!q.IsEmpty())
                    {
                        a.OnNext(default(T));
                    }

                    if (d)
                    {
                        var ex = error;
                        if (ex == null)
                        {
                            a.OnComplete();
                        }
                        else
                        {
                            a.OnError(ex);
                        }
                        return;
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
                }
            }

            void DrainNormal()
            {
                int missed = 1;
                var a = actual;
                var q = queue;
                var e = emitted;

                for (;;)
                {
                    long r = Volatile.Read(ref requested);

                    while (e != r)
                    {
                        if (Volatile.Read(ref cancelled) != 0)
                        {
                            q.Clear();
                            return;
                        }

                        bool d = Volatile.Read(ref done);
                        bool empty = !q.Poll(out T v);

                        if (d && empty)
                        {
                            var ex = error;
                            if (ex == null)
                            {
                                a.OnComplete();
                            }
                            else
                            {
                                a.OnError(ex);
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
                        if (Volatile.Read(ref cancelled) != 0)
                        {
                            q.Clear();
                            return;
                        }

                        if (Volatile.Read(ref done) && q.IsEmpty())
                        {
                            var ex = error;
                            if (ex == null)
                            {
                                a.OnComplete();
                            }
                            else
                            {
                                a.OnError(ex);
                            }
                            return;
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
                }
            }

            internal bool IsFull()
            {
                return queue.IsFull();
            }

            internal void OnNext(T item)
            {
                queue.Offer(item);
                Schedule();
            }

            internal void OnError(Exception cause)
            {
                error = cause;
                Volatile.Write(ref done, true);
                Schedule();
            }

            internal void OnComplete()
            {
                Volatile.Write(ref done, true);
                Schedule();
            }

            internal bool IsCancelled()
            {
                return Volatile.Read(ref cancelled) != 0;
            }
        }
    }
}
