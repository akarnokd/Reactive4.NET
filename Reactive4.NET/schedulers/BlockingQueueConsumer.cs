using Reactive4.NET.utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Reactive4.NET.subscribers
{
    sealed class BlockingQueueConsumer
    {
        static readonly Action ShutdownAction = () => { };

        readonly ISimpleQueue<Action> queue;

        readonly bool daemon;

        int shutdown;

        long wip;

        internal BlockingQueueConsumer(int capacityHint, bool daemon = true)
        {
            this.queue = new MpscLinkedArrayQueue<Action>(capacityHint);
            this.daemon = daemon;
        }

        internal void Shutdown()
        {
            if (Interlocked.Exchange(ref shutdown, 1) == 0)
            {
                Offer(ShutdownAction);
            }
        }

        internal bool Offer(Action task)
        {
            if (Volatile.Read(ref shutdown) == 0)
            {
                queue.Offer(task);
                if (Interlocked.Increment(ref wip) == 1)
                {
                    Monitor.Enter(this);
                    try
                    {
                        Monitor.Pulse(this);
                    }
                    finally
                    {
                        Monitor.Exit(this);
                    }
                }
                return true;
            }
            return false;
        }

        internal void Run()
        {
            Thread.CurrentThread.IsBackground = daemon;
            var sh = ShutdownAction;
            var q = queue;
            long missed = 0L;
            for (;;)
            {
                if (Volatile.Read(ref shutdown) != 0)
                {
                    q.Clear();
                    break;
                }
                Action a = null;

                for (int i = 0; i < 64; i++)
                {
                    if (q.Poll(out a))
                    {
                        break;
                    }
                }

                if (a != null)
                {
                    if (a == sh)
                    {
                        q.Clear();
                        break;
                    }
                    try
                    {
                        a();
                    }
                    catch
                    {
                        // TODO what to do with these?
                    }
                }
                else
                {
                    long w = Volatile.Read(ref wip);
                    if (w == missed)
                    {
                        missed = Interlocked.Add(ref wip, -missed);
                        if (missed == 0)
                        {
                            if (Monitor.TryEnter(this))
                            {
                                try
                                {
                                    while ((missed = Volatile.Read(ref wip)) == 0)
                                    {
                                        Monitor.Wait(this);
                                    }
                                }
                                finally
                                {
                                    Monitor.Exit(this);
                                }
                            }
                        }
                    }
                    else
                    {
                        missed = w;
                    }
                }
            }
        }

    }
}
