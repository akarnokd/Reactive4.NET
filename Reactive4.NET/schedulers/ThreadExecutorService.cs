using Reactive4.NET.utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Reactive4.NET.schedulers
{
    sealed class ThreadExecutorService : IExecutorService
    {
        internal static readonly IExecutorService Instance = new ThreadExecutorService();

        public long Now => SchedulerHelper.NowUTC();

        static long index;

        public IDisposable Schedule(Action task)
        {
            return SchedulerHelper.ScheduleTask(task);
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
            // this type of IExecutorService doesn't support the operation
        }

        public void Start()
        {
            // this type of IExecutorService doesn't support the operation
        }

        public IExecutorWorker Worker => new SingleExecutorWorker(
            new SingleThreadedExecutor("ThreadExecutorWorker-" + Interlocked.Increment(ref index)), 
            e => e.Shutdown());

    }
}
