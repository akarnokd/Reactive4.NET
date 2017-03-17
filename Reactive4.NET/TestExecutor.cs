using Reactive4.NET.utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Reactive4.NET
{
    /// <summary>
    /// Synchronous IExecutorService that allows moving a virtual time ahead in
    /// a programmatic fashion and execute delayed tasks without actually waiting
    /// for the time to pass.
    /// </summary>
    public sealed class TestExecutor : IExecutorService
    {
        /// <summary>
        /// Returns a worker that schedules tasks into the common synchronous queue.
        /// </summary>
        public IExecutorWorker Worker => new TestExecutorWorker(this);

        long currentTime;

        long id;

        readonly SortedSet<TestDelayedTask> queue;

        /// <summary>
        /// Constructs a TestExecutor.
        /// </summary>
        public TestExecutor()
        {
            this.queue = new SortedSet<TestDelayedTask>();
        }

        /// <summary>
        /// Returns the current time in milliseconds since the unix epoch.
        /// </summary>
        public long Now => currentTime;

        /// <summary>
        /// Schedule a task for non-delayed execution.
        /// </summary>
        /// <param name="task">The task to schedule.</param>
        /// <returns>The IDisposable that allows cancelling the execution of the task.</returns>
        public IDisposable Schedule(Action task)
        {
            TestDelayedTask tt = new TestDelayedTask(task, Now, NewId(), this, null);
            Add(tt);
            return tt;
        }

        /// <summary>
        /// Schedule a task for a delayed execution.
        /// </summary>
        /// <param name="task">The task to schedule.</param>
        /// <param name="delay">The delay amount.</param>
        /// <returns>The IDisposable that allows cancelling the execution of the task.</returns>
        public IDisposable Schedule(Action task, TimeSpan delay)
        {
            TestDelayedTask tt = new TestDelayedTask(task, 
                Now + (long)delay.TotalMilliseconds, NewId(), this, null);
            Add(tt);
            return tt;
        }

        /// <summary>
        /// Schedule a task for periodic execution with a fixed rate.
        /// </summary>
        /// <param name="task">The task to schedule.</param>
        /// <param name="initialDelay">The initial delay amount.</param>
        /// <param name="period">The repeat period.</param>
        /// <returns>The IDisposable that allows cancelling the execution of the task.</returns>
        public IDisposable Schedule(Action task, TimeSpan initialDelay, TimeSpan period)
        {
            TestDelayedTask tt = null;

            tt = new TestDelayedTask(() =>
            {
                task();
                tt.time += (long)period.TotalMilliseconds;
                tt.id = NewId();
                Add(tt);
            }, Now + (long)initialDelay.TotalMilliseconds, NewId(), this, null);
            Add(tt);
            return tt;
        }

        void Add(TestDelayedTask ttask)
        {
            lock (this)
            {
                queue.Add(ttask);
            }
        }

        void Remove(TestDelayedTask ttask)
        {
            lock (this)
            {
                queue.Remove(ttask);
            }
        }

        internal void RemoveAll(object token)
        {
            lock (this)
            {
                queue.RemoveWhere(tt => tt.token == token);
            }
        }

        /// <summary>
        /// Clears the internal task queue.
        /// </summary>
        public void Shutdown()
        {
            lock (this)
            {
                queue.Clear();
            }
        }

        /// <summary>
        /// TestExecutor doesn't support this operation and this method does nothing.
        /// </summary>
        public void Start()
        {
            // deliberately no-op
        }

        /// <summary>
        /// Advances the current time by the given amount and executes
        /// tasks (old and new) during this time.
        /// </summary>
        /// <param name="time">The time amount to move the current time ahead.</param>
        public void AdvanceTimeBy(TimeSpan time)
        {
            RunTasks(currentTime + (long)time.TotalMilliseconds);
        }

        /// <summary>
        /// Runs tasks that have an execution due-time less than or equal
        /// to the current time.
        /// </summary>
        public void RunTasks()
        {
            RunTasks(currentTime);
        }

        void RunTasks(long timeLimit)
        {
            for (;;)
            {
                TestDelayedTask tt;
                lock (this)
                {
                    if (queue.Count == 0)
                    {
                        break;
                    }
                    tt = queue.First();
                    if (tt.time > timeLimit)
                    {
                        break;
                    }
                    queue.Remove(tt);
                }

                currentTime = tt.time;
                tt.Run();
            }
            currentTime = timeLimit;
        }

        /// <summary>
        /// Returns true if there are still pending tasks present.
        /// </summary>
        public bool HasTasks {
            get
            {
                lock (this)
                {
                    return queue.Count != 0;
                }
            }
        }

        long NewId()
        {
            return Interlocked.Increment(ref id);
        }

        sealed class TestExecutorWorker : IExecutorWorker
        {
            readonly TestExecutor parent;

            int disposed;

            internal TestExecutorWorker(TestExecutor parent)
            {
                this.parent = parent;
            }

            public void Dispose()
            {
                if (Interlocked.Exchange(ref disposed, 1) == 0)
                {
                    parent.RemoveAll(this);
                }
            }

            public long Now => parent.Now;

            public IDisposable Schedule(Action task)
            {
                if (Volatile.Read(ref disposed) != 0)
                {
                    return EmptyDisposable.Instance;
                }
                TestDelayedTask tt = new TestDelayedTask(task, parent.Now, parent.NewId(), parent, this);
                parent.Add(tt);
                if (Volatile.Read(ref disposed) != 0)
                {
                    parent.Remove(tt);
                    return EmptyDisposable.Instance;
                }
                return tt;
            }

            public IDisposable Schedule(Action task, TimeSpan delay)
            {
                if (Volatile.Read(ref disposed) != 0)
                {
                    return EmptyDisposable.Instance;
                }
                TestDelayedTask tt = new TestDelayedTask(task, 
                    parent.Now + (long)delay.TotalMilliseconds, parent.NewId(), parent, this);
                parent.Add(tt);
                if (Volatile.Read(ref disposed) != 0)
                {
                    parent.Remove(tt);
                    return EmptyDisposable.Instance;
                }
                return tt;
            }

            public IDisposable Schedule(Action task, TimeSpan initialDelay, TimeSpan period)
            {
                TestDelayedTask tt = null;

                tt = new TestDelayedTask(() =>
                {
                    task();
                    tt.time += (long)period.TotalMilliseconds;
                    tt.id = parent.NewId();
                    parent.Add(tt);
                }, Now + (long)initialDelay.TotalMilliseconds, parent.NewId(), parent, this);
                parent.Add(tt);
                return tt;
            }
        }

        sealed class TestDelayedTask : IComparable<TestDelayedTask>, IDisposable
        {
            readonly Action task;

            internal long time;

            internal long id;

            readonly TestExecutor parent;

            internal readonly object token;

            bool cancelled;

            internal TestDelayedTask(Action task, long time, long id, TestExecutor parent, object token)
            {
                this.task = task;
                this.time = time;
                this.id = id;
                this.parent = parent;
                this.token = token;
            }

            public int CompareTo(TestDelayedTask other)
            {
                if (time < other.time)
                {
                    return -1;
                }
                if (time == other.time)
                {
                    return id < other.id ? -1 : (id > other.id ? 1 : 0);
                }
                return 1;
            }

            public void Run()
            {
                if (!Volatile.Read(ref cancelled))
                {
                    task();
                }
            }

            public void Dispose()
            {
                Volatile.Write(ref cancelled, true);
                parent.Remove(this);
            }
        }
        
    }
}
