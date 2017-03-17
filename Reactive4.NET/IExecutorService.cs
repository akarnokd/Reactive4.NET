using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reactive4.NET
{
    /// <summary>
    /// Represents an API to schedule a task immediately, with delay or periodically
    /// and provides the notion of a current time.
    /// </summary>
    public interface IActionScheduler
    {
        /// <summary>
        /// Schedule a task for non-delayed execution.
        /// </summary>
        /// <param name="task">The task to schedule.</param>
        /// <returns>The IDisposable that allows cancelling the execution of the task.</returns>
        IDisposable Schedule(Action task);

        /// <summary>
        /// Schedule a task for a delayed execution.
        /// </summary>
        /// <param name="task">The task to schedule.</param>
        /// <param name="delay">The delay amount.</param>
        /// <returns>The IDisposable that allows cancelling the execution of the task.</returns>
        IDisposable Schedule(Action task, TimeSpan delay);

        /// <summary>
        /// Schedule a task for periodic execution with a fixed rate.
        /// </summary>
        /// <param name="task">The task to schedule.</param>
        /// <param name="initialDelay">The initial delay amount.</param>
        /// <param name="period">The repeat period.</param>
        /// <returns>The IDisposable that allows cancelling the execution of the task.</returns>
        IDisposable Schedule(Action task, TimeSpan initialDelay, TimeSpan period);

        /// <summary>
        /// Returns the current time in milliseconds since the unix epoch.
        /// </summary>
        long Now { get; }
    }

    /// <summary>
    /// Abstraction over an async boundary that allows tasks to be scheduled
    /// with or without delay (but no ordering guarantees).
    /// </summary>
    public interface IExecutorService : IActionScheduler
    {
        /// <summary>
        /// Restarts this IExecutorService after it has shut down.
        /// </summary>
        /// <remarks>Not all IExecutorService implementations support this operation.</remarks>
        void Start();

        /// <summary>
        /// Shuts down this IExecutorService.
        /// </summary>
        void Shutdown();

        /// <summary>
        /// Returns a worker that guarantees non-overlapping FIFO execution for non-delayed tasks.
        /// </summary>
        IExecutorWorker Worker { get; }
    }

    /// <summary>
    /// Abstraction over an async boundary that allows tasks to be scheduled
    /// with or without delay and non-delayed tasks execute in a non-overlapping FIFO fashion.
    /// </summary>
    public interface IExecutorWorker : IActionScheduler, IDisposable
    {

    }
}
