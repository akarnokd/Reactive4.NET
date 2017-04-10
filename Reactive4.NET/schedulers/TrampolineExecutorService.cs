using Reactive4.NET.utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Reactive4.NET.schedulers
{
    sealed class TrampolineExecutorService : IExecutorService
    {
        internal static readonly IExecutorService Instance = new TrampolineExecutorService();

        public IExecutorWorker Worker => new TrampolineExecutorWorker();

        public long Now => SchedulerHelper.NowUTC();

        public IDisposable Schedule(Action task)
        {
            task();
            return EmptyDisposable.Instance;
        }

        public IDisposable Schedule(Action task, TimeSpan delay)
        {
            return SchedulerHelper.ScheduleTask(task, delay);
        }

        public IDisposable Schedule(Action task, TimeSpan initialDelay, TimeSpan period)
        {
            return SchedulerHelper.ScheduleTask(task, initialDelay, period);
        }

        public void Shutdown()
        {
            // not supported by this IExecutorService
        }

        public void Start()
        {
            // not supported by this IExecutorService
        }

        sealed class TrampolineExecutorWorker : IExecutorWorker
        {
            public long Now => SchedulerHelper.NowUTC();

            readonly ISimpleQueue<InterruptibleAction> queue;

            long wip;

            int cancelled;

            internal TrampolineExecutorWorker()
            {
                queue = new MpscLinkedArrayQueue<InterruptibleAction>(Flowable.BufferSize());
            }

            public void Dispose()
            {
                if (Interlocked.CompareExchange(ref cancelled, 1, 0) == 0)
                {
                    Drain();
                }
            }

            public IDisposable Schedule(Action task)
            {
                if (Volatile.Read(ref cancelled) != 0)
                {
                    return EmptyDisposable.Instance;
                }
                var t = new InterruptibleAction(task);
                queue.Offer(t);
                Drain();
                return t;
            }

            public IDisposable Schedule(Action task, TimeSpan delay)
            {
                if (Volatile.Read(ref cancelled) != 0)
                {
                    return EmptyDisposable.Instance;
                }
                var cts = new CancellationTokenSource();

                var t = new InterruptibleAction(task);
                cts.Token.Register(t.Dispose);

                Task.Delay(delay, cts.Token).ContinueWith(a =>
                {
                    queue.Offer(t);
                    Drain();
                }, cts.Token);

                return cts;
            }

            public IDisposable Schedule(Action task, TimeSpan initialDelay, TimeSpan period)
            {
                var cts = new CancellationTokenSource();

                var t = new InterruptibleAction(task);
                cts.Token.Register(t.Dispose);

                SchedulerHelper.ScheduleTask(() =>
                {
                    if (!t.IsDisposed)
                    {
                        queue.Offer(t);
                        Drain();
                    }
                }, initialDelay, period, cts);

                return cts;
            }

            void Drain()
            {
                if (Interlocked.Increment(ref wip) != 1L)
                {
                    return;
                }

                var q = queue;
                long missed = 1L;
                for (;;)
                {
                    while (q.Poll(out InterruptibleAction a))
                    {
                        if (Volatile.Read(ref cancelled) != 0)
                        {
                            try
                            {
                                a.Run();
                            }
                            catch
                            {
                                // TODO what to do with these?
                            }
                        }
                    }
                    long w = Volatile.Read(ref wip);
                    if  (w == missed)
                    {
                        missed = Interlocked.Add(ref wip, -missed);
                        if (missed == 0L)
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
