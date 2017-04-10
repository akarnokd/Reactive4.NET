using Reactive4.NET.utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Reactive4.NET.schedulers
{
    sealed class SharedExecutorService : IExecutorService
    {
        readonly IExecutorWorker shared;

        public long Now => shared.Now;

        internal SharedExecutorService(IExecutorWorker shared)
        {
            this.shared = shared;
        }

        public IDisposable Schedule(Action task)
        {
            return shared.Schedule(task);
        }

        public IDisposable Schedule(Action task, TimeSpan delay)
        {
            return shared.Schedule(task, delay);
        }

        public IDisposable Schedule(Action task, TimeSpan initialDelay, TimeSpan period)
        {
            return shared.Schedule(task, initialDelay, period);
        }

        public void Shutdown()
        {
            // Not supported by this type of Executor service
        }

        public void Start()
        {
            // Not supported by this type of Executor service
        }

        public IExecutorWorker Worker => new SharedExecutorWorker(shared);

        sealed class SharedExecutorWorker : IExecutorWorker, IWorkerServices
        {
            readonly IExecutorWorker shared;

            public long Now => shared.Now;

            int disposed;

            HashSet<InterruptibleAction> tasks;

            internal SharedExecutorWorker(IExecutorWorker shared)
            {
                this.tasks = new HashSet<InterruptibleAction>();
                this.shared = shared;
            }

            public bool AddAction(InterruptibleAction action)
            {
                if (Volatile.Read(ref disposed) == 0)
                {
                    lock (this)
                    {
                        var set = tasks;
                        if (set != null)
                        {
                            set.Add(action);
                            return true;
                        }
                    }
                }
                return false;
            }

            public void DeleteAction(InterruptibleAction action)
            {
                if (Volatile.Read(ref disposed) == 0)
                {
                    lock (this)
                    {
                        var set = tasks;
                        if (set != null)
                        {
                            set.Remove(action);
                        }
                    }
                }
            }

            public void Dispose()
            {
                if (Interlocked.CompareExchange(ref disposed, 1, 0) == 0)
                {
                    HashSet<InterruptibleAction> set;
                    lock (this)
                    {
                        set = tasks;
                        tasks = null;
                    }

                    if (set != null)
                    {
                        foreach (var ia in set)
                        {
                            ia.Dispose();
                        }
                    }
                }
            }

            public IDisposable Schedule(Action task)
            {
                if (Volatile.Read(ref disposed) == 0)
                {
                    InterruptibleAction ia = new InterruptibleAction(task);
                    ia.parent = this;
                    if (AddAction(ia))
                    {
                        var d = shared.Schedule(ia.Run);
                        DisposableHelper.Replace(ref ia.resource, d);
                        return ia;
                    }
                }
                return EmptyDisposable.Instance;
            }

            public IDisposable Schedule(Action task, TimeSpan delay)
            {
                if (Volatile.Read(ref disposed) == 0)
                {
                    InterruptibleAction ia = new InterruptibleAction(task);
                    ia.parent = this;
                    if (AddAction(ia))
                    {
                        var d = shared.Schedule(ia.Run, delay);
                        DisposableHelper.Replace(ref ia.resource, d);
                        return ia;
                    }
                }
                return EmptyDisposable.Instance;
            }

            public IDisposable Schedule(Action task, TimeSpan initialDelay, TimeSpan period)
            {
                if (Volatile.Read(ref disposed) == 0)
                {
                    InterruptibleAction ia = new InterruptibleAction(task, true);
                    ia.parent = this;
                    if (AddAction(ia))
                    {
                        var d = shared.Schedule(ia.Run, initialDelay, period);
                        DisposableHelper.Replace(ref ia.resource, d);
                        return ia;
                    }
                }
                return EmptyDisposable.Instance;
            }
        }
    }
}
