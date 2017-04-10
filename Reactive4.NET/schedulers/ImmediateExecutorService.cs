using Reactive4.NET.utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reactive4.NET.schedulers
{
    internal sealed class ImmediateExecutorService : IExecutorService, IExecutorWorker
    {
        internal static readonly ImmediateExecutorService Instance = new ImmediateExecutorService();

        public IExecutorWorker Worker => this;

        private ImmediateExecutorService() { }

        public void Dispose()
        {
            // ignored
        }

        public long Now => SchedulerHelper.NowUTC();

        public IDisposable Schedule(Action task)
        {
            task();
            return EmptyDisposable.Instance;
        }

        public IDisposable Schedule(Action task, TimeSpan delay)
        {
            throw new InvalidOperationException("Should not be called!");
        }

        public IDisposable Schedule(Action task, TimeSpan initialDelay, TimeSpan period)
        {
            throw new InvalidOperationException("Should not be called!");
        }

        public void Shutdown()
        {
            // no-op
        }

        public void Start()
        {
            // no-op
        }
    }
}
