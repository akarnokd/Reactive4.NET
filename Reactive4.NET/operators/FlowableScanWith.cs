using Reactive.Streams;
using Reactive4.NET.utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Reactive4.NET.operators
{
    sealed class FlowableScanWith<T, R> : AbstractFlowableOperator<T, R>
    {
        readonly Func<R> initialSupplier;

        readonly Func<R, T, R> scanner;

        readonly int bufferSize;

        public FlowableScanWith(IFlowable<T> source, Func<R> initialSupplier, Func<R, T, R> scanner, int bufferSize) : base(source)
        {
            this.initialSupplier = initialSupplier;
            this.scanner = scanner;
            this.bufferSize = bufferSize;
        }

        public override void Subscribe(IFlowableSubscriber<R> subscriber)
        {
            R r;

            try
            {
                r = initialSupplier();
            }
            catch (Exception ex)
            {
                subscriber.OnSubscribe(EmptySubscription<R>.Instance);
                subscriber.OnError(ex);
                return;
            }

            source.Subscribe(new ScanWithSubscriber(subscriber, r, scanner, bufferSize));
        }

        sealed class ScanWithSubscriber : IFlowableSubscriber<T>, IQueueSubscription<R>
        {
            readonly IFlowableSubscriber<R> actual;

            readonly Func<R, T, R> scanner;

            readonly int bufferSize;

            readonly int limit;

            readonly ISimpleQueue<R> queue;

            ISubscription upstream;

            R accumulator;

            int wip;

            bool done;
            bool cancelled;
            Exception error;

            long requested;

            long emitted;

            int consumed;

            bool outputFused;

            internal ScanWithSubscriber(IFlowableSubscriber<R> actual, R initial, Func<R, T, R> scanner, int bufferSize)
            {
                this.actual = actual;
                this.scanner = scanner;
                this.bufferSize = bufferSize;
                this.limit = bufferSize - (bufferSize >> 2);
                this.queue = new SpscArrayQueue<R>(bufferSize);
                this.accumulator = initial;
                this.queue.Offer(initial);
            }

            public void Cancel()
            {
                Volatile.Write(ref cancelled, true);
                upstream.Cancel();
                if (!outputFused && Interlocked.Increment(ref wip) == 1)
                {
                    Clear();
                }
            }

            public void Clear()
            {
                queue.Clear();
                accumulator = default(R);
            }

            public bool IsEmpty()
            {
                return queue.IsEmpty();
            }

            public bool Offer(R item)
            {
                throw new InvalidOperationException("Should not be called!");
            }

            public void OnComplete()
            {
                if (done)
                {
                    return;
                }
                Volatile.Write(ref done, true);
                Drain();
            }

            public void OnError(Exception cause)
            {
                if (done)
                {
                    return;
                }
                error = cause;
                Volatile.Write(ref done, true);
                Drain();
            }

            public void OnNext(T element)
            {
                if (done)
                {
                    return;
                }
                R acc;

                try
                {
                    acc = scanner(accumulator, element);
                }
                catch (Exception ex)
                {
                    upstream.Cancel();
                    OnError(ex);
                    return;
                }
                accumulator = acc;
                queue.Offer(acc);
                Drain();
            }

            public void OnSubscribe(ISubscription subscription)
            {
                if (SubscriptionHelper.Validate(ref upstream, subscription))
                {
                    actual.OnSubscribe(this);

                    var b = bufferSize - 1;
                    if (b != 0)
                    {
                        subscription.Request(b);
                    }
                }
            }

            public bool Poll(out R item)
            {
                if (queue.Poll(out item))
                {
                    var lim = limit;
                    if (++consumed == lim)
                    {
                        consumed = 0;
                        upstream.Request(lim);
                    }
                    return true;
                }
                return false;
            }

            public void Request(long n)
            {
                if (SubscriptionHelper.Validate(n))
                {
                    SubscriptionHelper.AddRequest(ref requested, n);
                    Drain();
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
                else {
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
                        a.OnNext(default(R));
                    }

                    if (d)
                    {
                        var ex = error;
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
                var lim = limit;
                var e = emitted;
                var c = consumed;

                for (;;)
                {
                    long r = Volatile.Read(ref requested);

                    while (e != r)
                    {
                        if (Volatile.Read(ref cancelled))
                        {
                            q.Clear();
                            accumulator = default(R);
                            return;
                        }

                        bool d = Volatile.Read(ref done);
                        bool empty = !q.Poll(out R t);

                        if (d && empty)
                        {
                            accumulator = default(R);
                            var ex = error;
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

                        a.OnNext(t);

                        e++;
                    }

                    if (e == r)
                    {
                        if (Volatile.Read(ref cancelled))
                        {
                            q.Clear();
                            accumulator = default(R);
                            return;
                        }
                        if (Volatile.Read(ref done) && q.IsEmpty())
                        {
                            accumulator = default(R);
                            var ex = error;
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
                        c = consumed;
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
