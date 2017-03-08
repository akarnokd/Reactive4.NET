using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Reactive.Streams;
using System.Threading;
using Reactive4.NET.utils;

namespace Reactive4.NET.operators
{
    sealed class FlowableObserveOn<T> : AbstractFlowableOperator<T, T>
    {
        readonly IExecutorService executor;

        readonly int bufferSize;

        public FlowableObserveOn(IFlowable<T> source, IExecutorService executor, int bufferSize) : base(source)
        {
            this.executor = executor;
            this.bufferSize = bufferSize;
        }

        public override void Subscribe(IFlowableSubscriber<T> subscriber)
        {
            source.Subscribe(new ObserveOnSubscriber(subscriber, executor.Worker, bufferSize));
        }

        internal sealed class ObserveOnSubscriber : IFlowableSubscriber<T>, IQueueSubscription<T>
        {
            readonly IFlowableSubscriber<T> actual;

            readonly IExecutorWorker worker;

            readonly int bufferSize;

            readonly int limit;

            ISubscription upstream;

            ISimpleQueue<T> queue;

            int sourceMode;

            bool outputMode;

            bool cancelled;
            bool done;
            Exception error;

            int wip;

            long requested;

            long emitted;

            int consumed;

            internal ObserveOnSubscriber(IFlowableSubscriber<T> actual, IExecutorWorker worker, int bufferSize)
            {
                this.actual = actual;
                this.worker = worker;
                this.bufferSize = bufferSize;
                this.limit = bufferSize - (bufferSize >> 2);
            }

            public void Cancel()
            {
                upstream.Cancel();
                worker.Dispose();
                if (Interlocked.Increment(ref wip) == 1)
                {
                    queue.Clear();
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

            public void OnComplete()
            {
                Volatile.Write(ref done, true);
                Schedule();
            }

            public void OnError(Exception cause)
            {
                error = cause;
                Volatile.Write(ref done, true);
                Schedule();
            }

            public void OnNext(T element)
            {
                if (sourceMode == FusionSupport.NONE)
                {
                    queue.Offer(element);
                }
                Schedule();
            }

            public void OnSubscribe(ISubscription subscription)
            {
                if (SubscriptionHelper.Validate(ref upstream, subscription))
                {
                    if (subscription is IQueueSubscription<T> qs)
                    {
                        int m = qs.RequestFusion(FusionSupport.ANY | FusionSupport.BARRIER);
                        if (m == FusionSupport.SYNC)
                        {
                            sourceMode = m;
                            queue = qs;
                            Volatile.Write(ref done, true);

                            actual.OnSubscribe(this);
                            return;
                        }
                        if (m == FusionSupport.ASYNC)
                        {
                            sourceMode = m;
                            queue = qs;

                            actual.OnSubscribe(this);

                            subscription.Request(bufferSize);
                            return;
                        }
                    }

                    queue = new SpscArrayQueue<T>(bufferSize);

                    actual.OnSubscribe(this);

                    subscription.Request(bufferSize);
                }
            }

            public bool Poll(out T item)
            {
                bool b = queue.Poll(out item);
                if (b && sourceMode != FusionSupport.SYNC)
                {
                    int c = consumed + 1;
                    if (c == limit)
                    {
                        consumed = 0;
                        upstream.Request(limit);
                    }
                    else
                    {
                        consumed = c;
                    }
                }
                return b;
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
                    outputMode = true;
                    return FusionSupport.ASYNC;
                }
                return FusionSupport.NONE;
            }

            void Schedule()
            {
                if (Interlocked.Increment(ref wip) == 1)
                {
                    worker.Schedule(Run);
                }
            }

            void Run()
            {
                if (outputMode)
                {
                    DrainFused();
                }
                else
                {
                    if (sourceMode == FusionSupport.SYNC)
                    {
                        DrainSync();
                    }
                    else
                    {
                        DrainAsync();
                    }
                }
            }

            void DrainFused()
            {
                int missed = 1;

                IFlowableSubscriber<T> a = actual;
                ISimpleQueue<T> q = queue;

                for (;;)
                {
                    if (Volatile.Read(ref cancelled))
                    {
                        queue.Clear();
                        return;
                    }

                    bool d = Volatile.Read(ref done);
                    bool empty = q.IsEmpty();

                    if (d && empty)
                    {
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
                    } else
                    if (!empty)
                    {
                        a.OnNext(default(T));
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

            void DrainSync()
            {
                int missed = 1;
                long e = emitted;
                IFlowableSubscriber<T> a = actual;
                ISimpleQueue<T> q = queue;

                for (;;)
                {
                    long r = Volatile.Read(ref requested);

                    while (e != r)
                    {
                        if (Volatile.Read(ref cancelled))
                        {
                            q.Clear();
                            return;
                        }

                        bool empty = !q.Poll(out T item);

                        if (empty)
                        {
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

                        a.OnNext(item);

                        e++;
                    }

                    if (e == r)
                    {
                        if (Volatile.Read(ref cancelled))
                        {
                            q.Clear();
                            return;
                        }

                        bool d = Volatile.Read(ref done);
                        bool empty = q.IsEmpty();

                        if (d && empty)
                        {
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

            void DrainAsync()
            {
                int missed = 1;
                long e = emitted;
                int f = consumed;
                int lim = limit;
                IFlowableSubscriber<T> a = actual;
                ISimpleQueue<T> q = queue;

                for (;;)
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
                        bool empty = !q.Poll(out T item);

                        if (d && empty)
                        {
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

                        a.OnNext(item);

                        e++;

                        if (++f == lim)
                        {
                            f = 0;
                            upstream.Request(lim);
                        }
                    }

                    if (e == r)
                    {
                        if (Volatile.Read(ref cancelled))
                        {
                            q.Clear();
                            return;
                        }

                        bool d = Volatile.Read(ref done);
                        bool empty = q.IsEmpty();

                        if (d && empty)
                        {
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
                        emitted = e;
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
                }
            }
        }
    }
}
