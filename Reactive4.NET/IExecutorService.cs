using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reactive4.NET
{
    public interface IExecutor
    {
        IDisposable Schedule(Action task);

        IDisposable Schedule(Action task, TimeSpan delay);

        IDisposable Schedule(Action task, TimeSpan initialDelay, TimeSpan period);

        long Now();
    }

    public interface IExecutorService : IExecutor
    {

        void Start();

        void Shutdown();

        IExecutorWorker Worker { get; }
    }

    public interface IExecutorWorker : IExecutor, IDisposable
    {

    }
}
