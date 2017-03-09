using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Reactive4.NET.operators
{
    internal sealed class FlowableRange : AbstractFlowableSource<int>
    {
        readonly int start;
        readonly int end;

        internal FlowableRange(int start, int end)
        {
            this.start = start;
            this.end = end;
        }

        public override void Subscribe(IFlowableSubscriber<int> subscriber)
        {
            if (subscriber is IConditionalSubscriber<int> s)
            {
                subscriber.OnSubscribe(new RangeConditionalSubscription(s, start, end));
            }
            else
            {
                subscriber.OnSubscribe(new RangeSubscription(subscriber, start, end));
            }
        }

        internal sealed class RangeSubscription : IQueueSubscription<int>
        {
            readonly IFlowableSubscriber<int> actual;

            readonly int end;

            int index;

            long requested;

            bool cancelled;

            internal RangeSubscription(IFlowableSubscriber<int> actual, int start, int end)
            {
                this.actual = actual;
                this.index = start;
                this.end = end;
            }

            public void Cancel()
            {
                Volatile.Write(ref cancelled, true);
            }

            public void Clear()
            {
                index = end;
            }

            public bool IsEmpty()
            {
                return index == end;
            }

            public bool Offer(int item)
            {
                throw new InvalidOperationException("Should not be called!");
            }

            public bool Poll(out int item)
            {
                int idx = index;
                if (idx == end)
                {
                    item = default(int);
                    return false;
                }
                item = idx;
                index = idx + 1;
                return true;
            }

            public void Request(long n)
            {
                if (n <= 0L)
                {
                    throw new ArgumentOutOfRangeException(nameof(n));
                }
                if (SubscriptionHelper.AddRequest(ref requested, n) == 0)
                {
                    int idx = index;
                    int f = end;
                    long e = 0;
                    IFlowableSubscriber<int> a = actual;

                    for (;;)
                    {

                        while (idx != f && e != n)
                        {
                            if (Volatile.Read(ref cancelled))
                            {
                                return;
                            }

                            a.OnNext(idx);

                            idx++;
                            e++;
                        }

                        if (idx == f)
                        {
                            if (!Volatile.Read(ref cancelled))
                            {
                                a.OnComplete();
                            }
                            return;
                        }

                        n = Volatile.Read(ref requested);
                        if (e == n)
                        {
                            index = idx;
                            n = Interlocked.Add(ref requested, -n);
                            if (n == 0L)
                            {
                                break;
                            }
                            e = 0L;
                        }
                    }
                }
            }

            public int RequestFusion(int mode)
            {
                return mode & FusionSupport.SYNC;
            }
        }

        internal sealed class RangeConditionalSubscription : IQueueSubscription<int>
        {
            readonly IConditionalSubscriber<int> actual;

            readonly int end;

            int index;

            long requested;

            bool cancelled;

            internal RangeConditionalSubscription(IConditionalSubscriber<int> actual, int start, int end)
            {
                this.actual = actual;
                this.index = start;
                this.end = end;
            }

            public void Cancel()
            {
                Volatile.Write(ref cancelled, true);
            }

            public void Clear()
            {
                index = end;
            }

            public bool IsEmpty()
            {
                return index == end;
            }

            public bool Offer(int item)
            {
                throw new InvalidOperationException("Should not be called!");
            }

            public bool Poll(out int item)
            {
                int idx = index;
                if (idx == end)
                {
                    item = default(int);
                    return false;
                }
                item = idx;
                index = idx + 1;
                return true;
            }

            public void Request(long n)
            {
                if (n <= 0L)
                {
                    throw new ArgumentOutOfRangeException(nameof(n));
                }
                if (SubscriptionHelper.AddRequest(ref requested, n) == 0)
                {
                    int idx = index;
                    int f = end;
                    long e = 0;
                    IConditionalSubscriber<int> a = actual;

                    for (;;)
                    {

                        while (idx != f && e != n)
                        {
                            if (Volatile.Read(ref cancelled))
                            {
                                return;
                            }

                            if (a.TryOnNext(idx))
                            {
                                e++;
                            }

                            idx++;
                        }

                        if (idx == f)
                        {
                            if (!Volatile.Read(ref cancelled))
                            {
                                a.OnComplete();
                            }
                            return;
                        }

                        n = Volatile.Read(ref requested);
                        if (e == n)
                        {
                            index = idx;
                            n = Interlocked.Add(ref requested, -n);
                            if (n == 0L)
                            {
                                break;
                            }
                            e = 0L;
                        }
                    }
                }
            }

            public int RequestFusion(int mode)
            {
                return mode & FusionSupport.SYNC;
            }
        }
    }
}
