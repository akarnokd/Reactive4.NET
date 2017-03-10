using Reactive4.NET.utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Reactive4.NET.schedulers
{
    sealed class ParallelExecutorService : IExecutorService
    {
        public long Now => SchedulerHelper.NowUTC();

        static readonly SingleThreadedExecutor[] ShutdownPool = new SingleThreadedExecutor[0];

        readonly int parallelism;

        SingleThreadedExecutor[] executors;

        int n;

        internal ParallelExecutorService() : this(Environment.ProcessorCount)
        {
        }

        internal ParallelExecutorService(int parallelism)
        {
            this.parallelism = parallelism;
            Start();
        }

        bool Pick(out SingleThreadedExecutor executor)
        {
            var x = Volatile.Read(ref executors);
            if (x == ShutdownPool)
            {
                executor = null;
                return false;
            }
            int idx = n;
            executor = x[idx];
            idx++;
            n = idx == parallelism ? 0 : idx;
            return true;
        }

        public IDisposable Schedule(Action task)
        {
            if (Pick(out SingleThreadedExecutor executor))
            {
                return executor.Schedule(task);
            }
            return EmptyDisposable.Instance;
        }

        public IDisposable Schedule(Action task, TimeSpan delay)
        {
            if (Pick(out SingleThreadedExecutor executor))
            {
                return executor.Schedule(task, delay);
            }
            return EmptyDisposable.Instance;
        }

        public IDisposable Schedule(Action task, TimeSpan initialDelay, TimeSpan period)
        {
            if (Pick(out SingleThreadedExecutor executor))
            {
                return executor.Schedule(task, initialDelay, period);
            }
            return EmptyDisposable.Instance;
        }

        public void Shutdown()
        {
            foreach (var executor in Interlocked.Exchange(ref executors, ShutdownPool))
            {
                executor.Shutdown();
            }
        }

        public void Start()
        {
            SingleThreadedExecutor[] ys = null; ;
            for (;;)
            {
                var xs = Volatile.Read(ref executors);
                if (xs != ShutdownPool)
                {
                    break;
                }
                if (ys == null)
                {
                    ys = new SingleThreadedExecutor[parallelism];
                    for (int i = 0; i < ys.Length; i++)
                    {
                        ys[i] = new SingleThreadedExecutor();
                    }
                }
                if (Interlocked.CompareExchange(ref executors, ys, ShutdownPool) == ShutdownPool)
                {
                    break;
                }
            }
        }

        public IExecutorWorker Worker {
            get
            {
                if (Pick(out SingleThreadedExecutor executor))
                {
                    return new SingleExecutorWorker(executor);
                }
                return SchedulerHelper.RejectingWorker;
            }
        }

    }
}
