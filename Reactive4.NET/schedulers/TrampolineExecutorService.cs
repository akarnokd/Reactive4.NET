using Reactive4.NET.utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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

            public void Dispose()
            {
                throw new NotImplementedException();
            }

            public IDisposable Schedule(Action task)
            {
                throw new NotImplementedException();
            }

            public IDisposable Schedule(Action task, TimeSpan delay)
            {
                throw new NotImplementedException();
            }

            public IDisposable Schedule(Action task, TimeSpan initialDelay, TimeSpan period)
            {
                throw new NotImplementedException();
            }
        }
    }
}
