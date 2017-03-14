using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Reactive4.NET.operators
{
    sealed class FlowableRepeatItem<T> : AbstractFlowableSource<T>
    {
        readonly T item;

        internal FlowableRepeatItem(T item)
        {
            this.item = item;
        }

        public override void Subscribe(IFlowableSubscriber<T> subscriber)
        {
            if (subscriber is IConditionalSubscriber<T> s)
            {
                subscriber.OnSubscribe(new RepeatItemConditionalSubscription(s, item));
            }
            else
            {
                subscriber.OnSubscribe(new RepeatItemSubscription(subscriber, item));
            }
        }

        abstract class AbstractRepeatItem : IQueueSubscription<T>
        {
            protected bool cleared;
            protected T item;

            protected bool cancelled;

            protected long requested;

            internal AbstractRepeatItem(T item)
            {
                this.item = item;
            }

            public void Cancel()
            {
                Volatile.Write(ref cancelled, true);
            }

            public void Clear()
            {
                item = default(T);
                cleared = true;
            }

            public bool IsEmpty()
            {
                return cleared;
            }

            public bool Offer(T item)
            {
                throw new InvalidOperationException("Should not be called");
            }

            public bool Poll(out T item)
            {
                if (cleared)
                {
                    item = default(T);
                    return false;
                }
                item = this.item;
                return true;
            }

            public int RequestFusion(int mode)
            {
                return mode & FusionSupport.SYNC;
            }

            public void Request(long n)
            {
                if (SubscriptionHelper.Validate(n))
                {
                    if (SubscriptionHelper.AddRequest(ref requested, n) == 0)
                    {
                        OnRequest(n);
                    }
                }
            }

            protected abstract void OnRequest(long n);
        }

        sealed class RepeatItemSubscription : AbstractRepeatItem
        {
            readonly IFlowableSubscriber<T> actual;
            

            internal RepeatItemSubscription(IFlowableSubscriber<T> actual, T item) : base(item)
            {
                this.actual = actual;
            }

            protected override void OnRequest(long n)
            {
                long e = 0L;
                var a = actual;
                var v = item;

                for (;;)
                {
                    while (e != n)
                    {
                        if (Volatile.Read(ref cancelled))
                        {
                            return;
                        }

                        a.OnNext(v);

                        e++;
                    }

                    n = Volatile.Read(ref requested);
                    if (e == n)
                    {
                        n = Interlocked.Add(ref requested, -e);
                        if (n == 0L)
                        {
                            break;
                        }
                        e = 0L;
                    }
                }
            }
        }

        sealed class RepeatItemConditionalSubscription : AbstractRepeatItem
        {
            readonly IConditionalSubscriber<T> actual;

            internal RepeatItemConditionalSubscription(IConditionalSubscriber<T> actual, T item) : base(item)
            {
                this.actual = actual;
            }

            protected override void OnRequest(long n)
            {
                long e = 0L;
                var a = actual;
                var v = item;

                for (;;)
                {
                    while (e != n)
                    {
                        if (Volatile.Read(ref cancelled))
                        {
                            return;
                        }

                        if (a.TryOnNext(v))
                        {
                            e++;
                        }
                    }

                    n = Volatile.Read(ref requested);
                    if (e == n)
                    {
                        n = Interlocked.Add(ref requested, -e);
                        if (n == 0L)
                        {
                            break;
                        }
                        e = 0L;
                    }
                }
            }
        }
    }
}
