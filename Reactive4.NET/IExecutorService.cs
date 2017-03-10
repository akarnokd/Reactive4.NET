using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reactive4.NET
{
    public interface IActionScheduler
    {
        IDisposable Schedule(Action task);

        IDisposable Schedule(Action task, TimeSpan delay);

        IDisposable Schedule(Action task, TimeSpan initialDelay, TimeSpan period);

        long Now { get; }
    }

    public interface IExecutorService : IActionScheduler
    {

        void Start();

        void Shutdown();

        IExecutorWorker Worker { get; }
    }

    public interface IExecutorWorker : IActionScheduler, IDisposable
    {

    }
}
