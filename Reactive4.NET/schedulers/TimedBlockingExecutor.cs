using Reactive4.NET.utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Reactive4.NET.schedulers
{
    sealed class TimedBlockingExecutor : IDisposable
    {
        readonly SortedSet<TimedTask> queue;

        readonly string name;

        long index;

        bool shutdown;

        int once;

        internal TimedBlockingExecutor(string name)
        {
            this.queue = new SortedSet<TimedTask>();
            this.name = name;
        }

        internal IDisposable Schedule(Action action, TimeSpan delay)
        {
            if (Volatile.Read(ref shutdown))
            {
                return EmptyDisposable.Instance;
            }
            long due = SchedulerHelper.NowUTC() + (long)delay.TotalMilliseconds;
            long id = Interlocked.Increment(ref index);

            var tt = new TimedTask(action, due, id, queue);
            if (Offer(tt))
            {
                return tt;
            }
            return EmptyDisposable.Instance;
        }

        internal IDisposable Schedule(Action action, TimeSpan initialDelay, TimeSpan period)
        {
            if (Volatile.Read(ref shutdown))
            {
                return EmptyDisposable.Instance;
            }

            SequentialDisposable inner = new SequentialDisposable();
            SequentialDisposable outer = new SequentialDisposable(inner);

            long due = SchedulerHelper.NowUTC() + (long)initialDelay.TotalMilliseconds;
            long id = Interlocked.Increment(ref index);

            long[] count = { 0 };

            Action recursive = null;
            recursive = () =>
            {
                action();
                var duePeriod = due + (long)(++count[0] * period.TotalMilliseconds);
                var idPeriod = Interlocked.Increment(ref index);
                var periodTT = new TimedTask(recursive, duePeriod, idPeriod, queue);
                if (Offer(periodTT))
                {
                    outer.Replace(periodTT);
                }
            };

            var tt = new TimedTask(recursive, due, id, queue);

            if (Offer(tt))
            {
                inner.Replace(tt);
                return outer;
            }

            return EmptyDisposable.Instance;
        }

        bool Offer(TimedTask tt)
        {
            lock (queue)
            {
                queue.Add(tt);
            }
            if (Volatile.Read(ref shutdown))
            {
                lock (queue)
                {
                    queue.Clear();
                }
                return false;
            }
            else
            {
                Monitor.Enter(this);
                Monitor.Pulse(this);
                Monitor.Exit(this);
            }
            return true;
        }

        internal void Start()
        {
            if (Volatile.Read(ref once) == 0 && Interlocked.CompareExchange(ref once, 1, 0) == 0)
            {
                Task.Factory.StartNew(Run, TaskCreationOptions.LongRunning);
            }
        }

        public void Dispose()
        {
            if (!Volatile.Read(ref shutdown))
            {
                Volatile.Write(ref shutdown, true);
                Monitor.Enter(this);
                Monitor.Pulse(this);
                Monitor.Exit(this);
            }
        }

        internal void Run()
        {
            Thread.CurrentThread.IsBackground = true;
            Thread.CurrentThread.Name = name;
            var q = queue;
            for (;;)
            {
                if (Volatile.Read(ref shutdown))
                {
                    lock (q)
                    {
                        q.Clear();
                    }
                    break;
                }
                else
                {
                    long now = SchedulerHelper.NowUTC();
                    TimedTask tt = null;
                    int next = 0;
                    lock (q)
                    {
                        tt = q.FirstOrDefault();
                        if (tt != null)
                        {
                            if (tt.due <= now)
                            {
                                q.Remove(tt);
                            }
                            else
                            if (Volatile.Read(ref tt.action) == null)
                            {
                                q.Remove(tt);
                                continue;
                            }
                            else
                            {
                                next = (int)Math.Max(0, tt.due - now);
                                tt = null;
                            }
                        }
                        else
                        {
                            next = int.MaxValue;
                        }
                    }

                    if (tt != null)
                    {
                        if (Volatile.Read(ref shutdown))
                        {
                            lock (q)
                            {
                                q.Clear();
                            }
                            return;
                        }
                        var a = Volatile.Read(ref tt.action);
                        try
                        {
                            a?.Invoke();
                        }
                        catch
                        {
                            // TODO what should happen here?
                        }
                    }
                    else
                    {
                        if (Volatile.Read(ref shutdown))
                        {
                            lock (q)
                            {
                                q.Clear();
                            }
                            return;
                        }
                        if (Monitor.TryEnter(this))
                        {
                            Monitor.Wait(this, next);
                            Monitor.Exit(this);
                        }
                    }
                }
            }
        }

        sealed class TimedTask : IComparable<TimedTask>, IDisposable
        {
            internal readonly long due;

            internal readonly long index;

            SortedSet<TimedTask> queue;

            internal Action action;

            internal TimedTask(Action action, long due, long index, SortedSet<TimedTask> queue)
            {
                this.action = action;
                this.due = due;
                this.index = index;
                this.queue = queue;
            }

            public int CompareTo(TimedTask other)
            {
                if (due == other.due)
                {
                    return index < other.index ? -1 : (index > other.index) ? 1 : 0;
                }
                return due < other.due ? -1 : 1;
            }

            public void Dispose()
            {
                if (Interlocked.Exchange(ref action, null) != null)
                {
                    var q = queue;
                    queue = null;

                    lock (q)
                    {
                        q.Remove(this);
                    }
                }
            }
        }
    }
}
