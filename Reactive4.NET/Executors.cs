using Reactive4.NET.schedulers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reactive4.NET
{
    /// <summary>
    /// Hosts default IExecutorService instances and allows
    /// creating customized ones.
    /// </summary>
    public static class Executors
    {
        /// <summary>
        /// An IExecutorSerivce that executes all tasks on
        /// a single dedicated thread.
        /// </summary>
        public static IExecutorService Single
        {
            get
            {
                return SingleExecutorService.Instance;
            }
        }

        /// <summary>
        /// An IExecutorService that executes tasks on a
        /// number of threads where the this number is equal
        /// to the available processor count.
        /// </summary>
        public static IExecutorService Computation {
            get
            {
                return ComputationExecutorService.Instance;
            }
        }

        /// <summary>
        /// An IExecutorService that executes tasks on a
        /// cached pool of threads where the number of
        /// cached threads is unlimited.
        /// </summary>
        public static IExecutorService IO
        {
            get
            {
                return IOExecutorService.Instance;
            }
        }

        /// <summary>
        /// An IExecutorService that creates new threads 
        /// for each direct task and for its worker.
        /// </summary>
        public static IExecutorService Thread
        {
            get
            {
                return ThreadExecutorService.Instance;
            }
        }

        /// <summary>
        /// An IExecutorService whose worker makes sure
        /// only one thread at a time executes tasks
        /// scheduled with it.
        /// </summary>
        public static IExecutorService Trampoline
        {
            get
            {
                return TrampolineExecutorService.Instance;
            }
        }

        /// <summary>
        /// An IExecutorService that uses the Task pool to
        /// execute tasks in a completely unordered fashion.
        /// </summary>
        public static IExecutorService Task
        {
            get {
                return TaskExecutorService.Instance;
            }
        }

        /// <summary>
        /// Special IExecutorService that doesn't support
        /// timed tasks and executes tasks on the caller's
        /// thread.
        /// </summary>
        internal static IExecutorService Immediate
        {
            get
            {
                return ImmediateExecutorService.Instance;
            }
        }

        // ----------------------------------------------------------------------------

        /// <summary>
        /// Creates a new single-threaded IExecutorService.
        /// </summary>
        /// <returns>The new IExecutorService instance.</returns>
        public static IExecutorService NewSingle()
        {
            return new SingleExecutorService();
        }

        /// <summary>
        /// Creates a new IExecutorService with a fixed
        /// number of threads equal to the number of
        /// available processors.
        /// </summary>
        /// <returns>The new IExecutorService instance.</returns>
        public static IExecutorService NewParallel()
        {
            return new ParallelExecutorService();
        }

        /// <summary>
        /// Creates a new IExecutorService with a provided
        /// fixed number of threads.
        /// </summary>
        /// <param name="parallelism">The parallelism level, positive.</param>
        /// <returns>The new IExecutorService instance.</returns>
        public static IExecutorService NewParallel(int parallelism)
        {
            return new ParallelExecutorService(parallelism);
        }

        /// <summary>
        /// Creates a new TestExecutor to help with synchronous
        /// operator testing.
        /// </summary>
        /// <returns>The new TestExecutor instance.</returns>
        public static TestExecutor NewTest()
        {
            return new TestExecutor();
        }

        /// <summary>
        /// Creates an IExecutorService that shares the given
        /// worker instance (from another IExecutorService).
        /// </summary>
        /// <param name="worker">The worker instance to use as a
        /// backing executor.</param>
        /// <returns>The new IExecutorService instance.</returns>
        public static IExecutorService NewShared(IExecutorWorker worker)
        {
            return null; // TODO return proper
        }

        /// <summary>
        /// Creates an IExecutorService which when started,
        /// uses the current thread and blocks for tasks to
        /// be executed on the current thread.
        /// To let the current thread go a call to Shutdown() is
        /// necessary.
        /// </summary>
        /// <returns>The new IExecutorService instance.</returns>
        public static IExecutorService NewBlocking()
        {
            return null; // TODO return proper
        }

        /// <summary>
        /// Creates an IExecutorService which when started,
        /// uses the current thread, executes the initial
        /// action and blocks for further tasks to
        /// be executed on the current thread.
        /// To let the current thread go a call to Shutdown() is
        /// necessary.
        /// </summary>
        /// <returns>The new IExecutorService instance.</returns>
        public static IExecutorService NewBlocking(Action initialTask)
        {
            return null; // TODO return proper
        }
    }
}
