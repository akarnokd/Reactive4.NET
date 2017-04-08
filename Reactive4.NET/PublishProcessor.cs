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
    /// A Processor that prefetches a bounded number of items from upstream
    /// and emits those items when all subscribers are ready to receive,
    /// establishing a lock-step behavior. Use the Start() method to setup
    /// this processor if it won't be subscribed to an IPublisher but will
    /// be used in an imperative style.
    /// </summary>
    /// <typeparam name="T">The input and output value type.</typeparam>
    public sealed class PublishProcessor<T> : IFlowableProcessor<T>, IDisposable
    {
        /// <summary>
        /// Indicates that this IFlowableProcessor has completed normally.
        /// </summary>
        public bool HasComplete => Volatile.Read(ref subscribers) == Terminated && error == null;

        /// <summary>
        /// Indicates that this IFlowableProcessor has terminated with an exception.
        /// </summary>
        public bool HasException => Volatile.Read(ref subscribers) == Terminated && error != null;

        /// <summary>
        /// Returns the terminal exception if HasException is true, null otherwise.
        /// </summary>
        public Exception Exception => Volatile.Read(ref subscribers) == Terminated ? error : null;

        /// <summary>
        /// Indicates there are any subscribers subscribed to this IFlowableProcessor.
        /// </summary>
        public bool HasSubscribers => Volatile.Read(ref subscribers).Length != 0;

        readonly int bufferSize;

        readonly int limit;

        ISubscription upstream;
        ISimpleQueue<T> queue;

        ProcessorSubscription[] subscribers = Empty;

        static readonly ProcessorSubscription[] Empty = new ProcessorSubscription[0];
        static readonly ProcessorSubscription[] Terminated = new ProcessorSubscription[0];

        int fusionMode;
        bool done;
        Exception error;

        int wip;

        int consumed;

        /// <summary>
        /// Creates a PublishProcessor with a default prefetch amount.
        /// </summary>
        public PublishProcessor() : this(Flowable.BufferSize())
        {

        }

        /// <summary>
        /// Constructs a PublishProcessor with the defined internal buffer size
        /// and prefetch amount.
        /// </summary>
        /// <param name="bufferSize">The buffer size and prefetch amount, positive.</param>
        public PublishProcessor(int bufferSize)
        {
            this.bufferSize = bufferSize;
            this.limit = bufferSize - (bufferSize >> 2);
        } 

        /// <summary>
        /// Initializes the PublishProcessor with the default buffer size and and
        /// empty ISubscription; this allows using the PublishProcessor without
        /// subscribing it to an IPublisher.
        /// </summary>
        public void Start()
        {
            if (SubscriptionHelper.SetOnce(ref upstream, EmptySubscription<T>.Instance))
            {
                Volatile.Write(ref queue, new SpscArrayQueue<T>(bufferSize));
            }
        }

        /// <summary>
        /// Successful terminal state.
        /// No further events will be sent even if Reactive.Streams.ISubscription.Request(System.Int64)
        /// is invoked again.
        /// </summary>
        public void OnComplete()
        {
            if (Interlocked.CompareExchange(ref error, ExceptionHelper.Terminated, null) == null)
            {
                Volatile.Write(ref done, true);
                Drain();
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
            if (Interlocked.CompareExchange(ref error, cause, null) == null)
            {
                Volatile.Write(ref done, true);
                Drain();
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
            if (fusionMode != FusionSupport.ASYNC)
            {
                queue.Offer(element);
            }
            Drain();
        }

        /// <summary>
        /// Tries to offer an item into the internal buffer and if
        /// that buffer is full, returns false.
        /// This should be called in a sequential manner similar to
        /// the OnXXX methods and never if this PublishProcessor is
        /// subscribed to an IProcessor.
        /// </summary>
        /// <param name="element">The item to signal to all subscribers.</param>
        /// <returns>True if buffered/emitted successfully, false if the internal
        /// buffer is full.</returns>
        public bool Offer(T element)
        {
            if (element == null)
            {
                throw new ArgumentNullException(nameof(element));
            }
            if (fusionMode != FusionSupport.ASYNC)
            {
                if (queue.Offer(element))
                {
                    Drain();
                    return true;
                }
            }
            return false;
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
                if (subscription is IQueueSubscription<T> qs)
                {
                    int m = qs.RequestFusion(FusionSupport.ANY);
                    if (m == FusionSupport.SYNC)
                    {
                        fusionMode = m;
                        Volatile.Write(ref queue, qs);
                        Volatile.Write(ref done, true);
                        Drain();
                        return;
                    }
                    if (m == FusionSupport.ASYNC)
                    {
                        fusionMode = m;
                        Volatile.Write(ref queue, qs);
                        subscription.Request(bufferSize);
                        return;
                    }
                }

                Volatile.Write(ref queue, new SpscArrayQueue<T>(bufferSize));

                subscription.Request(bufferSize);
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

        void Drain()
        {
            if (Interlocked.Increment(ref wip) != 1)
            {
                return;
            }

            int missed = 1;
            int f = consumed;
            int lim = limit;
            var q = Volatile.Read(ref queue);

            for (;;)
            {
                if (q != null)
                {
                    var s = Volatile.Read(ref subscribers);
                    int n = s.Length;
                    if (n != 0)
                    {
                        long r = -1L;
                        for (int i = 0; i < n; i++)
                        {
                            var ra = s[i].Requested();
                            if (ra >= 0L)
                            {
                                if (r == -1L)
                                {
                                    r = ra;
                                }
                                else
                                {
                                    r = Math.Min(r, ra);
                                }
                            }
                        }
                        bool changed = false;

                        while (r > 0L)
                        {
                            if (Volatile.Read(ref subscribers) != s)
                            {
                                changed = true;
                                break;
                            }
                            bool d = Volatile.Read(ref done);
                            bool empty = !q.Poll(out T v);

                            if (d && empty)
                            {
                                Exception ex = error;
                                if (ex != null && ex != ExceptionHelper.Terminated)
                                {
                                    foreach (var inner in Interlocked.Exchange(ref subscribers, Terminated))
                                    {
                                        inner.OnError(ex);
                                    }
                                } else
                                {
                                    foreach (var inner in Interlocked.Exchange(ref subscribers, Terminated))
                                    {
                                        inner.OnComplete();
                                    }
                                }
                                return;
                            }

                            if (empty)
                            {
                                break;
                            }

                            foreach (var inner in s)
                            {
                                inner.OnNext(v);
                            }
                            
                            r--;
                            if (fusionMode != FusionSupport.SYNC)
                            {
                                if (++f == lim)
                                {
                                    f = 0;
                                    upstream.Request(lim);
                                }
                            }
                        }

                        if (changed)
                        {
                            continue;
                        }

                        if (r == 0)
                        {
                            if (Volatile.Read(ref subscribers) != s)
                            {
                                continue;
                            }
                            if (Volatile.Read(ref done) && q.IsEmpty())
                            {
                                Exception ex = error;
                                if (ex != null && ex != ExceptionHelper.Terminated)
                                {
                                    foreach (var inner in Interlocked.Exchange(ref subscribers, Terminated))
                                    {
                                        inner.OnError(ex);
                                    }
                                }
                                else
                                {
                                    foreach (var inner in Interlocked.Exchange(ref subscribers, Terminated))
                                    {
                                        inner.OnComplete();
                                    }
                                }
                                return;
                            }
                        }
                    }
                }
                int w = Volatile.Read(ref wip);
                if (w == missed)
                {
                    consumed = f;
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
                if (q == null)
                {
                    q = Volatile.Read(ref queue);
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
                Drain();
            }
            else
            {
                Exception ex = error;
                if (ex != null && ex != ExceptionHelper.Terminated)
                {
                    ps.OnError(ex);
                }
                else
                {
                    ps.OnComplete();
                }
            }
        }

        /// <summary>
        /// Cancels the upstream ISubscription and signals and
        /// OperationCanceledException to current and future subscribers.
        /// </summary>
        public void Dispose()
        {
            SubscriptionHelper.Cancel(ref upstream);
            OnError(new OperationCanceledException());
        }

        /// <summary>
        /// Returns true if the upstream ISubscription has been disposed.
        /// </summary>
        public bool IsDisposed => SubscriptionHelper.IsCancelled(ref upstream);

        sealed class ProcessorSubscription : ISubscription
        {
            readonly IFlowableSubscriber<T> actual;

            readonly PublishProcessor<T> parent;

            long requested;

            long emitted;

            internal ProcessorSubscription(IFlowableSubscriber<T> actual, PublishProcessor<T> parent)
            {
                this.actual = actual;
                this.parent = parent;
            }

            internal bool IsCancelled()
            {
                return Volatile.Read(ref requested) == long.MinValue;
            }

            internal long Requested()
            {
                return Volatile.Read(ref requested) - emitted;
            }
            public void Cancel()
            {
                if (Interlocked.Exchange(ref requested, long.MinValue) != long.MinValue)
                {
                    parent.Remove(this);
                    parent.Drain();
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
                            return;
                        }
                        long u = r + n;
                        if (u < 0L)
                        {
                            u = long.MaxValue;
                        }
                        if (Interlocked.CompareExchange(ref requested, u, r) == r)
                        {
                            parent.Drain();
                            break;
                        }
                    }
                }
            }

            internal void OnNext(T element)
            {
                if (Volatile.Read(ref requested) != long.MinValue)
                {
                    emitted++;
                    actual.OnNext(element);
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
        }
    }
}
