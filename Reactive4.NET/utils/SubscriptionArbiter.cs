using Reactive.Streams;
using Reactive4.NET.operators;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Reactive4.NET.utils
{
    class SubscriptionArbiter : ISubscription
    {
        ISubscription current;

        long requested;

        int wip;

        ISubscription missedSubscription;

        long missedRequested;

        long missedProduced;

        bool cancelled;

        internal bool ArbiterIsCancelled()
        {
            return Volatile.Read(ref cancelled);
        }

        public virtual void Cancel()
        {
            if (!Volatile.Read(ref cancelled))
            {
                Volatile.Write(ref cancelled, true);
                ArbiterDrain();
            }
        }

        public void Request(long n)
        {
            if (n <= 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(n));
            }
            SubscriptionHelper.AddRequest(ref missedRequested, n);
            ArbiterDrain();
        }

        public void ArbiterSet(ISubscription next)
        {
            Volatile.Write(ref missedSubscription, next);
            ArbiterDrain();
        }

        public void ArbiterProduced(long n)
        {
            if (n <= 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(n));
            }
            Interlocked.Add(ref missedProduced, n);
            ArbiterDrain();
        }

        void ArbiterDrain()
        {
            if (Interlocked.Increment(ref wip) != 1)
            {
                return;
            }
            ArbiterDrainLoop();
        }

        void ArbiterDrainLoop()
        {
            ISubscription target = null;
            long req = 0L;

            int missed = 1;
            for (;;)
            {
                long mr = Volatile.Read(ref missedRequested);
                if (mr != 0L)
                {
                    mr = Interlocked.Exchange(ref missedRequested, 0L);
                }
                long mp = Volatile.Read(ref missedProduced);
                if (mp != 0L)
                {
                    mp = Interlocked.Exchange(ref missedProduced, 0L);
                }
                ISubscription ms = Volatile.Read(ref missedSubscription);
                if (ms != null)
                {
                    ms = Interlocked.Exchange(ref missedSubscription, null);
                }
                bool c = Volatile.Read(ref cancelled);

                if (c)
                {
                    current?.Cancel();
                    ms?.Cancel();
                    current = null;
                    target = null;
                }
                else
                {
                    long r = requested;
                    if (r != long.MaxValue)
                    {
                        long u = r + mr;
                        if (u < 0L)
                        {
                            u = long.MaxValue;
                        }
                        if (u != long.MaxValue)
                        {
                            long v = u - mp;
                            if (v < 0L)
                            {
                                v = 0L;
                            }
                            requested = v;
                            r = v;
                        }
                        else
                        {
                            requested = u;
                            r = u;
                        }
                    }
                    if (ms == null)
                    {
                        target = current;
                        req += mr;
                        if (req < 0L)
                        {
                            req = long.MaxValue;
                        }
                    } else
                    {
                        current = ms;
                        target = ms;
                        req = r;
                    }
                }

                int w = Volatile.Read(ref wip);
                if (w == missed)
                {
                    missed = Interlocked.Add(ref wip, -missed);
                    if (missed == 0)
                    {
                        break;
                    }
                }
                else
                {
                    missed = w;
                }
            }
            if (target != null && req != 0L)
            {
                target.Request(req);
            }
        }
    }
}
