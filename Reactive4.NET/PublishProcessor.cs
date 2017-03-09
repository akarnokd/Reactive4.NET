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
    public sealed class PublishProcessor<T> : IFlowableProcessor<T>, IDisposable
    {
        public bool HasComplete
        {
            get
            {
                return Volatile.Read(ref subscribers) == Terminated && error == null;
            }
        }

        public bool HasException
        {
            get
            {
                return Volatile.Read(ref subscribers) == Terminated && error != null;
            }
        }

        public Exception Exception
        {
            get
            {
                return Volatile.Read(ref subscribers) == Terminated ? error : null;
            }
        }

        public bool HasSubscribers
        {
            get
            {
                return Volatile.Read(ref subscribers).Length != 0;
            }
        }

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
        int once;

        int wip;

        int consumed;

        public PublishProcessor() : this(Flowable.BufferSize())
        {

        }

        public PublishProcessor(int bufferSize)
        {
            this.bufferSize = bufferSize;
            this.limit = bufferSize - (bufferSize >> 2);
        } 

        public void Start()
        {
            if (SubscriptionHelper.SetOnce(ref upstream, EmptySubscription<T>.Instance))
            {
                Volatile.Write(ref queue, new SpscArrayQueue<T>(bufferSize));
            }
        }

        public void OnComplete()
        {
            if (Interlocked.CompareExchange(ref error, ExceptionHelper.Terminated, null) == null)
            {
                Volatile.Write(ref done, true);
                Drain();
            }
        }

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

        public void OnSubscribe(ISubscription subscription)
        {
            if (SubscriptionHelper.SetOnce(ref upstream, subscription))
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
                        long r = s[0].Requested();
                        for (int i = 1; i < n; i++)
                        {
                            r = Math.Min(r, s[i].Requested());
                        }

                        while (r != 0L)
                        {
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
                            if (++f == lim)
                            {
                                f = 0;
                                if (fusionMode != FusionSupport.SYNC)
                                {
                                    upstream.Request(lim);
                                }
                            }
                        }

                        if (r == 0)
                        {
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

        public void Dispose()
        {
            SubscriptionHelper.Cancel(ref upstream);
            OnError(new OperationCanceledException());
        }

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
                    actual.OnNext(element);
                    emitted++;
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
