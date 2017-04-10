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
    sealed class BlockingExecutorService : IExecutorService
    {
        public long Now => SchedulerHelper.NowUTC();

        readonly TimedBlockingExecutor timed;

        readonly BlockingQueueConsumer runner;

        static long index;

        int state;

        internal BlockingExecutorService(string name, bool ownTimer, bool daemon)
        {
            if (name == null)
            {
                name = "BlockingExecutorService-" + Interlocked.Increment(ref index);
            }

            runner = new BlockingQueueConsumer(Flowable.BufferSize(), name, daemon);

            if (ownTimer)
            {
                this.timed = new TimedBlockingExecutor(name + ":Timed");
            }
            else
            {
                this.timed = TimedExecutorPool.TimedExecutor;
            }
        }

        public IDisposable Schedule(Action task)
        {
            return Schedule(task, null);
        }

        public IDisposable Schedule(Action task, TimeSpan delay)
        {
            return Schedule(task, delay, null);
        }

        public IDisposable Schedule(Action task, TimeSpan initialDelay, TimeSpan period)
        {
            return Schedule(task, initialDelay, period, null);
        }

        IDisposable Schedule(Action task, IWorkerServices worker = null)
        {
            if (Volatile.Read(ref state) != 2)
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

        IDisposable Schedule(Action task, TimeSpan delay, IWorkerServices worker = null)
        {
            if (Volatile.Read(ref state) != 2)
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

        IDisposable Schedule(Action task, TimeSpan initialDelay, TimeSpan period, IWorkerServices worker = null)
        {
            if (Volatile.Read(ref state) != 2)
            {
                var run = runner;

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

        public void Shutdown()
        {
            if (Interlocked.CompareExchange(ref state, 2, 1) == 1)
            {
                runner.Shutdown();
            }
        }

        public void Start()
        {
            if (Interlocked.CompareExchange(ref state, 1, 0) == 0)
            {
                runner.Run();
            }
        }

        public IExecutorWorker Worker => new BlockingExecutorWorker(this);

        sealed class BlockingExecutorWorker : IExecutorWorker, IWorkerServices
        {
            readonly BlockingExecutorService executor;

            int disposed;

            HashSet<InterruptibleAction> tasks;

            public long Now => SchedulerHelper.NowUTC();

            internal BlockingExecutorWorker(BlockingExecutorService executor)
            {
                this.executor = executor;
                this.tasks = new HashSet<InterruptibleAction>();
            }

            public bool AddAction(InterruptibleAction action)
            {
                if (Volatile.Read(ref disposed) == 0)
                {
                    lock (this)
                    {
                        var set = tasks;
                        if (set != null)
                        {
                            set.Add(action);
                            return true;
                        }
                    }
                }
                return false;
            }

            public void DeleteAction(InterruptibleAction action)
            {
                if (Volatile.Read(ref disposed) == 0)
                {
                    lock (this)
                    {
                        var set = tasks;
                        if (set != null)
                        {
                            set.Remove(action);
                        }
                    }
                }
            }

            public void Dispose()
            {
                if (Interlocked.CompareExchange(ref disposed, 1, 0) == 0)
                {
                    HashSet<InterruptibleAction> set;
                    lock (this)
                    {
                        set = tasks;
                        tasks = null;
                    }

                    if (set != null)
                    {
                        foreach (var ia in set)
                        {
                            ia.Dispose();
                        }
                    }
                }
            }

            public IDisposable Schedule(Action task)
            {
                return executor.Schedule(task, this);
            }

            public IDisposable Schedule(Action task, TimeSpan delay)
            {
                return executor.Schedule(task, delay, this);
            }

            public IDisposable Schedule(Action task, TimeSpan initialDelay, TimeSpan period)
            {
                return executor.Schedule(task, initialDelay, period, this);
            }
        }
    }
}
