using Reactive4.NET.utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Reactive4.NET.operators
{
    sealed class FlowableInterval : AbstractFlowableSource<long>
    {
        readonly IExecutorService executor;

        readonly TimeSpan initialDelay;

        readonly TimeSpan period;

        internal FlowableInterval(TimeSpan initialDelay, TimeSpan period, IExecutorService executor)
        {
            this.executor = executor;
            this.initialDelay = initialDelay;
            this.period = period;
        }

        public override void Subscribe(IFlowableSubscriber<long> subscriber)
        {
            var parent = new IntervalSubscription(subscriber);
            subscriber.OnSubscribe(parent);

            parent.SetTask(executor.Schedule(parent.Run, initialDelay, period));
        }

        sealed class IntervalSubscription : IQueueSubscription<long>
        {
            readonly IFlowableSubscriber<long> actual;

            IDisposable task;

            long requested;

            long available;

            long emitted;

            int wip;

            bool outputFused;

            internal IntervalSubscription(IFlowableSubscriber<long> actual)
            {
                this.actual = actual;
            }

            public void Cancel()
            {
                DisposableHelper.Dispose(ref task);
            }

            internal void SetTask(IDisposable d)
            {
                DisposableHelper.Replace(ref task, d);
            }

            public void Clear()
            {
                emitted = Volatile.Read(ref available);
            }

            public bool IsEmpty()
            {
                return Volatile.Read(ref available) == emitted;
            }

            public bool Offer(long item)
            {
                throw new InvalidOperationException("Should not be called");
            }

            public bool Poll(out long item)
            {
                long e = emitted;
                if (Volatile.Read(ref available) == e)
                {
                    item = default(long);
                    return false;
                }
                item = e;
                emitted = e + 1;
                return true;
            }

            public void Request(long n)
            {
                if (SubscriptionHelper.Validate(n))
                {
                    SubscriptionHelper.AddRequest(ref requested, n);
                    if (outputFused)
                    {
                        DrainFused();
                    }
                    else
                    {
                        Drain();
                    }
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

            internal void Run()
            {
                Interlocked.Increment(ref available);
                if (outputFused)
                {
                    DrainFused();
                }
                else
                {
                    Drain();
                }
            }

            internal void DrainFused()
            {
                if (Interlocked.Increment(ref wip) != 1)
                {
                    return;
                }

                int missed = 1;
                for (;;)
                {
                    if (Volatile.Read(ref task) == DisposableHelper.Disposed)
                    {
                        return;
                    }

                    long q = Volatile.Read(ref available);
                    long e = emitted;

                    if (e != q)
                    {
                        e++;
                        actual.OnNext(default(long));
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

            internal void Drain()
            {
                if (Interlocked.Increment(ref wip) != 1)
                {
                    return;
                }

                int missed = 1;
                long e = emitted;
                var a = actual;

                for (;;)
                {
                    long r = Volatile.Read(ref requested);
                    long q = Volatile.Read(ref available);

                    while (e != r && e < q)
                    {
                        if (Volatile.Read(ref task) == DisposableHelper.Disposed)
                        {
                            return;
                        }

                        a.OnNext(e);

                        e++;
                    }

                    if (e == r || e == q)
                    {
                        if (Volatile.Read(ref task) == DisposableHelper.Disposed)
                        {
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
        }
    }
}
