using Reactive4.NET.utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Reactive4.NET.schedulers
{
    sealed class SingleExecutorService : IExecutorService
    {
        internal static readonly IExecutorService Instance = new SingleExecutorService();

        public long Now => SchedulerHelper.NowUTC();

        SingleThreadedExecutor executor;

        static long index;

        readonly string name;

        internal SingleExecutorService(string name = "SingleExecutorWorker")
        {
            this.name = name;
            executor = new SingleThreadedExecutor(name);
        }

        public IDisposable Schedule(Action task)
        {
            var x = Volatile.Read(ref executor);
            if (x != null)
            {
                return x.Schedule(task);
            }
            return EmptyDisposable.Instance;
        }

        public IDisposable Schedule(Action task, TimeSpan delay)
        {
            var x = Volatile.Read(ref executor);
            if (x != null)
            {
                return x.Schedule(task, delay);
            }
            return EmptyDisposable.Instance;
        }

        public IDisposable Schedule(Action task, TimeSpan initialDelay, TimeSpan period)
        {
            var x = Volatile.Read(ref executor);
            if (x != null)
            {
                return executor.Schedule(task, initialDelay, period);
            }
            return EmptyDisposable.Instance;
        }

        public void Shutdown()
        {
            Interlocked.Exchange(ref executor, null)?.Shutdown();
        }

        public void Start()
        {
            SingleThreadedExecutor ys = null;
            for (;;)
            {
                var xs = Volatile.Read(ref executor);
                if (xs != null)
                {
                    break;
                }
                if (ys == null)
                {
                    ys = new SingleThreadedExecutor(name + "-" + (Interlocked.Increment(ref index)));
                }
                if (Interlocked.CompareExchange(ref executor, ys, xs) == xs)
                {
                    break;
                }
            }
        }

        public IExecutorWorker Worker { 
            get
            {
                var x = Volatile.Read(ref executor);
                if (x != null)
                {
                    return new SingleExecutorWorker(executor);
                }
                return SchedulerHelper.RejectingWorker;
            }
        }
    }
}
