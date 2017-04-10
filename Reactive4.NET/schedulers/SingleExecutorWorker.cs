using Reactive4.NET.utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Reactive4.NET.schedulers
{
    /// <summary>
    /// Standard, tracking, single-threaded IExecutorWorker that runs with an
    /// underlying SingleThreadedExecutor and optionally calls an onShutdown
    /// action when it is disposed.
    /// </summary>
    internal sealed class SingleExecutorWorker : IExecutorWorker, IWorkerServices
    {
        readonly SingleThreadedExecutor executor;

        readonly Action<SingleThreadedExecutor> onShutdown;

        int disposed;

        HashSet<InterruptibleAction> tasks;

        internal SingleExecutorWorker(SingleThreadedExecutor executor, Action<SingleThreadedExecutor> onShutdown = null)
        {
            this.executor = executor;
            this.onShutdown = onShutdown;
            this.tasks = new HashSet<InterruptibleAction>();
        }

        public long Now => SchedulerHelper.NowUTC();

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
                onShutdown?.Invoke(executor);

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
