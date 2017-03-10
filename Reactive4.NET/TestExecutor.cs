using Reactive4.NET.utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Reactive4.NET
{
    public sealed class TestExecutor : IExecutorService
    {
        public IExecutorWorker Worker => throw new NotImplementedException();

        long currentTime;

        long id;

        readonly SortedSet<TestDelayedTask> queue;

        public TestExecutor()
        {
            this.queue = new SortedSet<TestDelayedTask>();
        }

        public long Now => currentTime;

        public IDisposable Schedule(Action task)
        {
            TestDelayedTask tt = new TestDelayedTask(task, Now, NewId(), this, null);
            Add(tt);
            return tt;
        }

        public IDisposable Schedule(Action task, TimeSpan delay)
        {
            TestDelayedTask tt = new TestDelayedTask(task, 
                Now + (long)delay.TotalMilliseconds, NewId(), this, null);
            Add(tt);
            return tt;
        }

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

        public void Shutdown()
        {
            lock (this)
            {
                queue.Clear();
            }
        }

        public void Start()
        {
            // deliberately no-op
        }

        public void AdvanceTimeBy(TimeSpan time)
        {
            RunTasks(currentTime + (long)time.TotalMilliseconds);
        }

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

        public bool HasTasks {
            get
            {
                lock (this)
                {
                    return queue.Count != 0;
                }
            }
        }

        public long NewId()
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
