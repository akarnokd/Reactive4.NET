using Reactive4.NET.utils;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Reactive4.NET.schedulers
{
    sealed class IOExecutorService : IExecutorService
    {
        internal static readonly IExecutorService Instance = new IOExecutorService();

        public long Now => SchedulerHelper.NowUTC();

        readonly object guard;

        readonly HashSet<SingleThreadedExecutor> executors;

        readonly TimedBlockingExecutor timed;

        static readonly ArrayQueue<Entry> ShutdownQueue = new ArrayQueue<Entry>();

        ArrayQueue<Entry> queue;

        long ttlMilliseconds;

        IDisposable cleanupTask;

        int state;

        static readonly int STATE_FRESH = 0;

        static readonly int STATE_STARTING = 1;

        static readonly int STATE_RUNNING = 2;

        static readonly int STATE_SHUTDOWN = 3;

        static long index;

        internal IOExecutorService()
        {
            guard = new object();
            queue = ShutdownQueue;
            executors = new HashSet<SingleThreadedExecutor>();
            timed = new TimedBlockingExecutor("IOExecutorService:Cleanup");
            ttlMilliseconds = 60 * 1000;
            Start();
        }

        /// <summary>
        /// The time to live value for the cached entries in milliseconds.
        /// </summary>
        internal long TimeToLive {
            get => Volatile.Read(ref ttlMilliseconds);
            set => Volatile.Write(ref ttlMilliseconds, value);
        }

        void Cleanup()
        {
            double now = Now;


            lock (guard)
            {
                var q = queue;
                for (;;)
                {
                    if (!q.Peek(out var ex))
                    {
                        break;
                    }
                    if (ex.due > now)
                    {
                        break;
                    }
                    q.Poll(out ex);
                }
            }
        }

        public IDisposable Schedule(Action task)
        {
            if (Pick(out var exec))
            {
                var iot = new IOTask(task, exec, this, false);
                iot.SetRunner(exec.Schedule(iot.Run));
                return iot;
            }
            return EmptyDisposable.Instance;
        }

        public IDisposable Schedule(Action task, TimeSpan delay)
        {
            if (Pick(out var exec))
            {
                var iot = new IOTask(task, exec, this, false);
                iot.SetRunner(exec.Schedule(iot.Run, delay));
                return iot;
            }
            return EmptyDisposable.Instance;
        }

        public IDisposable Schedule(Action task, TimeSpan initialDelay, TimeSpan period)
        {
            if (Pick(out var exec))
            {
                var iot = new IOTask(task, exec, this, true);
                iot.SetRunner(exec.Schedule(iot.Run, initialDelay, period));
                return iot;
            }
            return EmptyDisposable.Instance;
        }

        public bool Pick(out SingleThreadedExecutor exec)
        {
            lock (guard)
            {
                var q = queue;
                if (q == ShutdownQueue)
                {
                    exec = null;
                    return false;
                }
                if (q.Poll(out var e))
                {
                    exec = e.executor;
                    return true;
                }
                var ex = new SingleThreadedExecutor("IOExecutorWorker-" + Interlocked.Increment(ref index));
                executors.Add(ex);
                exec = ex;
                return true;
            }
        }

        public void Release(SingleThreadedExecutor exec)
        {
            long due = Now + TimeToLive;
            var e = new Entry(exec, due);
            lock (guard)
            {
                var q = queue;
                if (q != ShutdownQueue && executors.Contains(exec))
                {
                    q.Offer(e);
                }
            }
        }

        public void Shutdown()
        {
            while (Volatile.Read(ref state) == STATE_STARTING) ;
            if (Interlocked.CompareExchange(ref state, STATE_SHUTDOWN, STATE_RUNNING) == STATE_RUNNING)
            {
                cleanupTask.Dispose();
                lock (guard)
                {
                    queue = ShutdownQueue;
                    foreach (var ex in executors)
                    {
                        ex.Shutdown();
                    }
                    executors.Clear();
                }
                Interlocked.Exchange(ref state, STATE_FRESH);
            }
        }

        public void Start()
        {
            if (Interlocked.CompareExchange(ref state, STATE_STARTING, STATE_FRESH) == STATE_FRESH)
            {
                lock (guard)
                {
                    queue = new ArrayQueue<Entry>();
                }
                cleanupTask = timed.Schedule(Cleanup, TimeSpan.FromMilliseconds(TimeToLive), TimeSpan.FromMilliseconds(TimeToLive));
                Interlocked.Exchange(ref state, STATE_RUNNING);
            }
        }

        public IExecutorWorker Worker { get
            {
                if (Pick(out var w))
                {
                    return new SingleExecutorWorker(w, Release);
                }
                return SchedulerHelper.RejectingWorker;
            }
        }

        internal class Entry
        {
            internal readonly SingleThreadedExecutor executor;
            internal readonly long due;

            internal Entry(SingleThreadedExecutor executor, long due)
            {
                this.executor = executor;
                this.due = due;
            }
        }

        internal class IOTask : IDisposable
        {
            readonly bool periodic;

            Action task;

            SingleThreadedExecutor executor;

            IOExecutorService service;

            IDisposable runner;

            internal IOTask(Action task, SingleThreadedExecutor executor, IOExecutorService service, bool periodic)
            {
                this.task = task;
                this.executor = executor;
                this.service = service;
                this.periodic = periodic;
            }

            public void Dispose()
            {
                DisposableHelper.Dispose(ref runner);
                Volatile.Write(ref task, null);
                var ex = Interlocked.Exchange(ref executor, null);
                if (ex != null)
                {
                    Interlocked.Exchange(ref service, null)?.Release(ex);
                }
            }

            internal void SetRunner(IDisposable d)
            {
                DisposableHelper.Replace(ref runner, d);
            }

            internal void Run()
            {
                try
                {
                    Volatile.Read(ref task)?.Invoke();
                    if (!periodic)
                    {
                        Dispose();
                    }
                }
                catch
                {
                    Dispose();
                    // What should happen to this exception?
                }
            }
        }
    }
}
