using Reactive4.NET.utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Reactive4.NET.schedulers
{
    internal sealed class TaskExecutorService : IExecutorService
    {

        internal static readonly TaskExecutorService Instance = new TaskExecutorService();

        public IExecutorWorker Worker => new TaskExecutorWorker();

        private TaskExecutorService() { }

        public long Now()
        {
            return SchedulerHelper.NowUTC();
        }

        public IDisposable Schedule(Action task)
        {
            var cts = new CancellationTokenSource();
            Task.Run(task, cts.Token);
            return cts;
        }

        public IDisposable Schedule(Action task, TimeSpan delay)
        {
            var cts = new CancellationTokenSource();
            Task.Delay(delay, cts.Token).ContinueWith(a => task(), cts.Token);
            return cts;
        }

        public IDisposable Schedule(Action task, TimeSpan initialDelay, TimeSpan period)
        {
            var cts = new CancellationTokenSource();

            Action<Task> recursive = null;
            long now = Now() + (long)initialDelay.TotalMilliseconds;
            long[] round = { 0 };
            recursive = t =>
            {
                task();
                long next = (long)(now + (++round[0]) * period.TotalMilliseconds - Now());
                Task.Delay(TimeSpan.FromMilliseconds(Math.Max(0L, next)), cts.Token).ContinueWith(recursive, cts.Token);
            };

            Task.Delay(initialDelay, cts.Token)
                .ContinueWith(recursive, cts.Token);

            return cts;
        }

        public void Shutdown()
        {
            // not supported with this type of IExecutorService
        }

        public void Start()
        {
            // not supported with this type of IExecutorService
        }

        internal sealed class TaskExecutorWorker : IExecutorWorker
        {
            readonly SetCompositeDisposable tasks;

            internal TaskExecutorWorker()
            {
                this.tasks = new SetCompositeDisposable();
            }

            public void Dispose()
            {
                tasks.Dispose();
            }

            public long Now()
            {
                return SchedulerHelper.NowUTC();
            }

            public IDisposable Schedule(Action task)
            {
                var dt = new DisposableTask(task, this);
                if (tasks.Add(dt))
                {
                    Task.Run((Action)dt.Run, dt.cts.Token);
                    return dt;
                }
                return EmptyDisposable.Instance;
            }

            public IDisposable Schedule(Action task, TimeSpan delay)
            {
                var dt = new DisposableTask(task, this);
                if (tasks.Add(dt))
                {
                    Task.Delay(delay, dt.cts.Token).ContinueWith(a => dt.Run(), dt.cts.Token);
                    return dt;
                }
                return EmptyDisposable.Instance;
            }

            public IDisposable Schedule(Action task, TimeSpan initialDelay, TimeSpan period)
            {
                var dt = new DisposablePeriodicTask(task, this, (long)(Now() + initialDelay.TotalMilliseconds), (long)period.TotalMilliseconds);
                if (tasks.Add(dt))
                {
                    Task.Delay(initialDelay, dt.cts.Token).ContinueWith(a => dt.Run(), dt.cts.Token);
                    return dt;
                }
                return EmptyDisposable.Instance;
            }

            internal sealed class DisposableTask : IDisposable
            {
                readonly Action task;

                readonly TaskExecutorWorker parent;

                internal readonly CancellationTokenSource cts;

                internal DisposableTask(Action task, TaskExecutorWorker parent)
                {
                    this.task = task;
                    this.parent = parent;
                    this.cts = new CancellationTokenSource();
                }

                public void Dispose()
                {
                    cts.Dispose();
                    parent.tasks.Delete(this);
                }

                internal void Run()
                {
                    try
                    {
                        task();
                    }
                    finally
                    {
                        parent.tasks.Delete(this);
                    }
                }
            }

            internal sealed class DisposablePeriodicTask : IDisposable
            {
                readonly Action task;

                readonly TaskExecutorWorker parent;

                internal readonly CancellationTokenSource cts;

                readonly long start;

                readonly long period;

                long count;

                internal DisposablePeriodicTask(Action task, TaskExecutorWorker parent, long start, long period)
                {
                    this.task = task;
                    this.parent = parent;
                    this.start = start;
                    this.period = period;
                    this.cts = new CancellationTokenSource();
                }

                public void Dispose()
                {
                    cts.Dispose();
                    parent.tasks.Delete(this);
                }

                internal void Run()
                {
                    try
                    {
                        task();

                        long next = Math.Max(0L, start + (++count) * period - parent.Now());
                        Task.Delay(TimeSpan.FromMilliseconds(next), cts.Token).ContinueWith(a => Run(), cts.Token);
                    }
                    catch
                    {
                        Dispose();
                        throw;
                    }
                }
            }
        }
    }
}
