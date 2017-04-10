using Reactive4.NET.subscribers;
using Reactive4.NET.utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Reactive4.NET.schedulers
{
    internal sealed class SingleThreadedExecutor : IDisposable
    {
        int state;

        static readonly int STATE_STARTED = 0;
        static readonly int STATE_RUNNING = 1;
        static readonly int STATE_SHUTDOWN = 2;

        BlockingQueueConsumer runner;

        public long Now => SchedulerHelper.NowUTC();

        readonly TimedBlockingExecutor timed;

        readonly string name;

        internal SingleThreadedExecutor(string name)
        {
            runner = new BlockingQueueConsumer(Flowable.BufferSize(), name);
            timed = TimedExecutorPool.TimedExecutor;
            this.name = name;
        }

        internal void Start()
        {
            if (Interlocked.CompareExchange(ref state, STATE_STARTED, STATE_SHUTDOWN) == STATE_SHUTDOWN)
            {
                Volatile.Write(ref runner, new BlockingQueueConsumer(Flowable.BufferSize(), name));
            }
        }

        internal void Shutdown()
        {
            if (Interlocked.Exchange(ref state, STATE_SHUTDOWN) != STATE_SHUTDOWN)
            {
                runner.Shutdown();
            }
        }

        public void Dispose()
        {
            Shutdown();
        }

        bool Prepare()
        {
            int s = Volatile.Read(ref state);
            if (s != STATE_SHUTDOWN)
            {
                if (s != STATE_RUNNING)
                {
                    if (Interlocked.CompareExchange(ref state, STATE_RUNNING, STATE_STARTED) == STATE_STARTED)
                    {
                        Task.Factory.StartNew(runner.Run, TaskCreationOptions.LongRunning);
                    }
                    else
                    {
                        return false;
                    }
                }
                return true;
            }
            return false;
        }

        public IDisposable Schedule(Action task, IWorkerServices worker = null)
        {
            if (Prepare())
            {
                InterruptibleAction ia = new InterruptibleAction(task);
                ia.parent = worker;
                if (worker == null || worker.AddAction(ia))
                {
                    if (runner.Offer(ia.Run))
                    {
                        return ia;
                    }
                }
            }
            return EmptyDisposable.Instance;
        }

        public IDisposable Schedule(Action task, TimeSpan delay, IWorkerServices worker = null)
        {
            if (Prepare())
            {
                var run = runner;
                var t = new InterruptibleAction(task);
                t.parent = worker;
                if (worker == null || worker.AddAction(t))
                {
                    var d = timed.Schedule(() =>
                    {
                        run.Offer(t.Run);
                    }, delay);

                    DisposableHelper.Replace(ref t.resource, d);

                    return t;
                }
            }
            return EmptyDisposable.Instance;
        }

        public IDisposable Schedule(Action task, TimeSpan initialDelay, TimeSpan period, IWorkerServices worker = null)
        {
            if (Prepare())
            {
                var run = runner;
                var cts = new CancellationTokenSource();

                var t = new InterruptibleAction(task, true);
                t.parent = worker;

                if (worker == null || worker.AddAction(t))
                {
                    var d = timed.Schedule(() =>
                    {
                        if (!t.IsDisposed)
                        {
                            run.Offer(t.Run);
                        }
                    }, initialDelay, period);
                    DisposableHelper.Replace(ref t.resource, d);

                    return t;
                }
            }
            return EmptyDisposable.Instance;
        }
    }
}
