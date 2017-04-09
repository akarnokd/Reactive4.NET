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
    sealed class ParallelFlowableFork<T> : AbstractParallelSource<T>
    {
        readonly IFlowable<T> source;

        readonly int parallelism;

        readonly int bufferSize;

        public override int Parallelism => parallelism;

        internal ParallelFlowableFork(IFlowable<T> source, int parallelism, int bufferSize)
        {
            this.source = source;
            this.parallelism = parallelism;
            this.bufferSize = bufferSize;
        }

        public override void Subscribe(IFlowableSubscriber<T>[] subscribers)
        {
            if (Validate(subscribers))
            {
                source.Subscribe(new ForkSubscriber(subscribers, bufferSize));
            }
        }

        sealed class ForkSubscriber : IFlowableSubscriber<T>
        {
            readonly Entry[] subscribers;

            readonly int bufferSize;

            readonly int limit;

            ISubscription upstream;

            ISimpleQueue<T> queue;

            int wip;

            int cancelled;

            bool done;
            Exception error;

            int fusionMode;

            int consumed;

            bool initDone;

            int index;

            internal ForkSubscriber(IFlowableSubscriber<T>[] subscribers, int bufferSize)
            {
                int n = subscribers.Length;
                var subs = new Entry[n];
                for (int i = 0; i < n; i++)
                {
                    subs[i].actual = subscribers[i];
                }
                this.subscribers = subs;
                this.bufferSize = bufferSize;
                this.limit = bufferSize - (bufferSize >> 2);
                this.cancelled = n;
            }

            public void OnComplete()
            {
                Volatile.Write(ref done, true);
                Drain();
            }

            public void OnError(Exception cause)
            {
                error = cause;
                Volatile.Write(ref done, true);
                Drain();
            }

            public void OnNext(T element)
            {
                if (fusionMode == FusionSupport.NONE)
                {
                    queue.Offer(element);
                }
                Drain();
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
                            fusionMode = m;
                            queue = qs;
                            Volatile.Write(ref done, true);

                            InitRails();
                            Drain();
                            return;
                        }

                        if (m == FusionSupport.ASYNC)
                        {
                            fusionMode = m;
                            queue = qs;

                            InitRails();

                            subscription.Request(bufferSize);
                            return;
                        }
                    }

                    queue = new SpscArrayQueue<T>(bufferSize);

                    InitRails();

                    subscription.Request(bufferSize);
                }
            }

            void InitRails()
            {
                var subs = subscribers;
                int n = subs.Length;
                for (int i = 0; i < n; i++)
                {
                    subs[i].actual.OnSubscribe(new RailSubscription(this, i));
                }
                Volatile.Write(ref initDone, true);
            }

            void Drain()
            {
                if (Interlocked.Increment(ref wip) == 1)
                {
                    if (fusionMode == FusionSupport.SYNC)
                    {
                        DrainSync();
                    }
                    else
                    {
                        DrainNormal();
                    }
                }
            }

            void DrainSync()
            {
                int missed = 1;
                var subs = subscribers;
                var n = subs.Length;
                var q = queue;
                var c = consumed;
                var idx = index;
                var lim = limit;

                for (;;)
                {

                    bool quit = false;
                    int notReady = 0;
                    for (;;)
                    {
                        var a = subs[idx].actual;
                        long r = Volatile.Read(ref subs[idx].requested);
                        long e = subs[idx].emitted;
                        bool x = Volatile.Read(ref subs[idx].cancelled) != 0;

                        if (x)
                        {
                            notReady++;
                        }
                        else
                        {
                            notReady = 0;
                            if (e != r)
                            {
                                bool empty = !q.Poll(out T t);

                                if (empty)
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
                                    notReady++;
                                    subs[idx].cancelled = 1;
                                    quit = true;
                                } else
                                {
                                    a.OnNext(t);
                                    subs[idx].emitted = e + 1;

                                    if (++c == lim)
                                    {
                                        c = 0;
                                        upstream.Request(lim);
                                    }
                                }
                            }
                            else
                            {
                                if (q.IsEmpty())
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
                                    notReady++;
                                    subs[idx].cancelled = 1;
                                    quit = true;
                                }
                                else
                                {
                                    notReady++;
                                }
                            }
                        }

                        if (notReady == n)
                        {
                            break;
                        }
                        idx++;
                        if (idx == n)
                        {
                            idx = 0;
                        }
                    }

                    if (quit)
                    {
                        return;
                    }

                    int w = Volatile.Read(ref wip);
                    if (w == missed)
                    {
                        consumed = c;
                        index = idx;
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
                var subs = subscribers;
                var n = subs.Length;
                var q = queue;
                var c = consumed;
                var idx = index;
                var lim = limit;

                for (;;)
                {

                    bool quit = false;
                    int notReady = 0;
                    for (;;)
                    {
                        var a = subs[idx].actual;
                        long r = Volatile.Read(ref subs[idx].requested);
                        long e = subs[idx].emitted;
                        bool x = Volatile.Read(ref subs[idx].cancelled) != 0;

                        if (x)
                        {
                            notReady++;
                        }
                        else
                        {
                            bool d = Volatile.Read(ref done);
                            if (e != r)
                            {
                                bool empty = !q.Poll(out T t);

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
                                    notReady++;
                                    subs[idx].cancelled = 1;
                                    quit = true;
                                } else
                                if (empty)
                                {
                                    notReady++;
                                }
                                else
                                {
                                    notReady = 0;

                                    a.OnNext(t);
                                    subs[idx].emitted = e + 1;

                                    if (++c == lim)
                                    {
                                        c = 0;
                                        upstream.Request(lim);
                                    }
                                }
                            }
                            else
                            {
                                bool empty = q.IsEmpty();
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
                                    notReady++;
                                    subs[idx].cancelled = 1;
                                    quit = true;
                                }
                                else
                                {
                                    notReady++;
                                }
                            }
                        }

                        if (notReady == n)
                        {
                            break;
                        }
                        idx++;
                        if (idx == n)
                        {
                            idx = 0;
                        }
                    }

                    if (quit)
                    {
                        return;
                    }

                    int w = Volatile.Read(ref wip);
                    if (w == missed)
                    {
                        consumed = c;
                        index = idx;
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

            void Cancel(int index)
            {
                if (Interlocked.CompareExchange(ref subscribers[index].cancelled, 1L, 0L) == 0L)
                {
                    if (Interlocked.Decrement(ref cancelled) == 0)
                    {
                        upstream.Cancel();
                    }
                }
            }

            void Request(int index, long n)
            {
                if (SubscriptionHelper.Validate(n))
                {
                    SubscriptionHelper.AddRequest(ref subscribers[index].requested, n);
                }
                if (Volatile.Read(ref initDone))
                {
                    Drain();
                }
            }

            internal class RailSubscription : ISubscription
            {
                readonly ForkSubscriber parent;

                readonly int index;

                internal RailSubscription(ForkSubscriber parent, int index)
                {
                    this.parent = parent;
                    this.index = index;
                }

                public void Cancel()
                {
                    parent.Cancel(index);
                }

                public void Request(long n)
                {
                    parent.Request(index, n);
                }
            }

            internal struct Entry
            {
                internal long requested;

                internal long emitted;

                internal long cancelled;

                internal IFlowableSubscriber<T> actual;
            }
        }
    }
}
