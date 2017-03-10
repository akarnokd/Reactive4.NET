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
        readonly Action action;

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

        internal InterruptibleAction(Action action)
        {
            this.action = action;
        }

        internal void Run()
        {
            Volatile.Write(ref runner, Thread.CurrentThread);
            if (Interlocked.CompareExchange(ref state, Running, Fresh) == Fresh)
            {
                action();
                if (Interlocked.CompareExchange(ref state, Completed, Running) == Running)
                {
                    runner = null;
                    parent?.DeleteAction(this);
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
                Thread.Yield();
            }

            if (Volatile.Read(ref state) == Interrupted)
            {
                try
                {
                    Thread.Sleep(int.MaxValue);
                }
                catch
                {
                    // Ignored
                }
            }
            runner = null;
            parent?.DeleteAction(this);
        }

        public void Dispose()
        {
            if (Interlocked.CompareExchange(ref state, Disposed, Fresh) != Fresh)
            {
                Thread r = runner;
                if (Interlocked.CompareExchange(ref state, Interrupting, Running) == Running)
                {
                    r?.Interrupt();
                    Volatile.Write(ref state, Interrupted);
                }
            }
            Interlocked.Exchange(ref parent, null)?.DeleteAction(this);
            Interlocked.Exchange(ref resource, null)?.Dispose();
        }

        internal bool Reset()
        {
            return Volatile.Read(ref state) == Fresh || Interlocked.CompareExchange(ref state, Fresh, Completed) == Completed;
        }
    }
}
