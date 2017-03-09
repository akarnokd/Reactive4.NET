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
    public sealed class UnicastProcessor<T> : IFlowableProcessor<T>
    {
        public bool HasComplete
        {
            get
            {
                return Volatile.Read(ref done) && error == null;
            }
        }

        public bool HasException
        {
            get
            {
                return Volatile.Read(ref done) && error != null;
            }
        }

        public Exception Exception
        {
            get
            {
                return Volatile.Read(ref done) ? error : null;
            }
        }

        public bool HasSubscribers
        {
            get
            {
                return Volatile.Read(ref actual) != null;
            }
        }

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

        public UnicastProcessor() : this(Flowable.BufferSize(), () => { })
        {

        }

        public UnicastProcessor(int bufferSize) : this(bufferSize, () => { })
        {

        }

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
