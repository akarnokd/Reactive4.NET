using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Reactive4.NET.operators
{
    sealed class FlowableRepeatFunction<T> : AbstractFlowableSource<T>
    {
        readonly Func<T> func;

        internal FlowableRepeatFunction(Func<T> func)
        {
            this.func = func;
        }

        public override void Subscribe(IFlowableSubscriber<T> subscriber)
        {
            if (subscriber is IConditionalSubscriber<T> s)
            {
                subscriber.OnSubscribe(new RepeatFunctionConditionalSubscription(s, func));
            }
            else
            {
                subscriber.OnSubscribe(new RepeatFunctionSubscription(subscriber, func));
            }
        }

        internal abstract class AbstractRepeatFunctionSubscription : IQueueSubscription<T>
        {
            protected bool cleared;
            protected Func<T> func;

            protected bool cancelled;

            protected long requested;

            internal AbstractRepeatFunctionSubscription(Func<T> func)
            {
                this.func = func;
            }
            public void Cancel()
            {
                Volatile.Write(ref cancelled, true);
            }

            public void Clear()
            {
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
                try
                {
                    item = func();
                }
                catch
                {
                    cleared = true;
                    throw;
                }
                return true;
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

            public int RequestFusion(int mode)
            {
                return mode & FusionSupport.SYNC;
            }

            protected abstract void OnRequest(long n);
        }

        sealed class RepeatFunctionSubscription : AbstractRepeatFunctionSubscription
        {
            readonly IFlowableSubscriber<T> actual;

            internal RepeatFunctionSubscription(IFlowableSubscriber<T> actual, Func<T> func) : base(func)
            {
                this.actual = actual;
            }

            protected override void OnRequest(long n)
            {
                long e = 0L;
                var a = actual;
                var f = func;

                for (;;)
                {
                    while (e != n)
                    {
                        if (Volatile.Read(ref cancelled))
                        {
                            return;
                        }

                        T v;

                        try
                        {
                            v = func();
                        }
                        catch (Exception ex)
                        {
                            cleared = true;
                            a.OnError(ex);
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

        sealed class RepeatFunctionConditionalSubscription : AbstractRepeatFunctionSubscription
        {
            readonly IConditionalSubscriber<T> actual;

            internal RepeatFunctionConditionalSubscription(IConditionalSubscriber<T> actual, Func<T> func) : base(func)
            {
                this.actual = actual;
            }

            protected override void OnRequest(long n)
            {
                long e = 0L;
                var a = actual;
                var f = func;

                for (;;)
                {
                    while (e != n)
                    {
                        if (Volatile.Read(ref cancelled))
                        {
                            return;
                        }

                        T v;

                        try
                        {
                            v = func();
                        }
                        catch (Exception ex)
                        {
                            cleared = true;
                            a.OnError(ex);
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
