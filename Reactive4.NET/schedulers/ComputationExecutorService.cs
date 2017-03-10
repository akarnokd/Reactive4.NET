using Reactive4.NET.utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reactive4.NET.schedulers
{
    sealed class ComputationExecutorService : IExecutorService
    {
        internal static readonly IExecutorService Instance = new ComputationExecutorService();

        public IExecutorWorker Worker => throw new NotImplementedException();

        public long Now => SchedulerHelper.NowUTC();

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

        public void Shutdown()
        {
            throw new NotImplementedException();
        }

        public void Start()
        {
            throw new NotImplementedException();
        }
    }
}
