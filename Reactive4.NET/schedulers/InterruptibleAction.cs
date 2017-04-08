using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Reactive4.NET.schedulers
{
    internal sealed class InterruptibleAction : IDisposable
    {
        readonly bool periodic;

        Action action;

        int state;
        static readonly int Fresh = 0;
        static readonly int Running = 1;
        static readonly int Completed = 2;
        static readonly int Interrupting = 3;
        static readonly int Interrupted = 4;
        static readonly int Disposed = 5;

        Thread runner;

        internal IWorkerServices parent;

        internal IDisposable resource;

        internal InterruptibleAction(Action action, bool periodic = false)
        {
            this.action = action;
            this.periodic = periodic;
        }

        internal void Run()
        {
            Volatile.Write(ref runner, Thread.CurrentThread);
            if (Interlocked.CompareExchange(ref state, Running, Fresh) == Fresh)
            {
                Volatile.Read(ref action)?.Invoke();
                if (Interlocked.CompareExchange(ref state, Completed, Running) == Running)
                {
                    runner = null;
                    if (!periodic)
                    {
                        action = null;
                        parent?.DeleteAction(this);
                    }
                    return;
                }
            }
            int count = 64;
            while (Volatile.Read(ref state) == Interrupting && count != 0)
            {
                count--;
            }

            while (Volatile.Read(ref state) == Interrupting)
            {
#if NETSTANDARD
                Thread.Sleep(0);
#else
                Thread.Yield();
#endif
            }

            if (Volatile.Read(ref state) == Interrupted)
            {
#if !NETSTANDARD
                try
                {
                    Thread.Sleep(int.MaxValue);
                }
                catch
                {
                    // Ignored
                }
#endif
            }
            runner = null;
            if (!periodic)
            {
                action = null;
                parent?.DeleteAction(this);
            }
        }

        public void Dispose()
        {
            if (Interlocked.CompareExchange(ref state, Disposed, Fresh) != Fresh)
            {
                Thread r = runner;
                if (Interlocked.CompareExchange(ref state, Interrupting, Running) == Running)
                {
#if !NETSTANDARD
                    r?.Interrupt();
#endif
                    Volatile.Write(ref state, Interrupted);
                }
            }
            Interlocked.Exchange(ref action, null);
            Interlocked.Exchange(ref parent, null)?.DeleteAction(this);
            Interlocked.Exchange(ref resource, null)?.Dispose();
        }

        internal bool Reset()
        {
            return Volatile.Read(ref state) == Fresh || Interlocked.CompareExchange(ref state, Fresh, Completed) == Completed;
        }
    }
}
