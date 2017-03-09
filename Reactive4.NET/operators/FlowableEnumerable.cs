using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Reactive4.NET.operators
{
    internal sealed class FlowableEnumerable<T> : AbstractFlowableSource<T>
    {
        readonly IEnumerable<T> enumerable;

        internal FlowableEnumerable(IEnumerable<T> enumerable)
        {
            this.enumerable = enumerable;
        }

        public override void Subscribe(IFlowableSubscriber<T> subscriber)
        {
            IEnumerator<T> enumerator = null;
            bool b;
            try
            {
                enumerator = enumerable.GetEnumerator();
                b = enumerator.MoveNext();
            }
            catch (Exception ex)
            {
                try
                {
                    enumerator?.Dispose();
                }
                catch (ObjectDisposedException)
                {
                    // can't do much about this
                }
                subscriber.OnSubscribe(EmptySubscription<T>.Instance);
                subscriber.OnError(ex);
                return;
            }

            if (!b)
            {
                try
                {
                    enumerator.Dispose();
                }
                catch (ObjectDisposedException)
                {
                    // can't do much about this
                }
                subscriber.OnSubscribe(EmptySubscription<T>.Instance);
                subscriber.OnComplete();
                return;
            }

            if (subscriber is IConditionalSubscriber<T> s)
            {
                subscriber.OnSubscribe(new EnumerableConditionalSubscription(s, enumerator));
            }
            else
            {
                subscriber.OnSubscribe(new EnumerableSubscription(subscriber, enumerator));
            }
        }

        internal abstract class AbstractEnumerableSubscription : IQueueSubscription<T>
        {
            protected readonly IEnumerator<T> enumerator;

            protected long requested;

            protected int cancelled;

            bool done;

            internal AbstractEnumerableSubscription(IEnumerator<T> enumerator)
            {
                this.enumerator = enumerator;
            }

            public void Cancel()
            {
                if (Interlocked.CompareExchange(ref cancelled, 1, 0) == 0)
                {
                    Request(1);
                }
            }

            public void Clear()
            {
                done = true;
                try
                {
                    enumerator.Dispose();
                }
                catch (ObjectDisposedException)
                {
                    // can't do much about this
                }
            }

            public bool IsEmpty()
            {
                return done;
            }

            public bool Offer(T item)
            {
                throw new InvalidOperationException("Should not be called!");
            }

            public bool Poll(out T item)
            {
                if (!done)
                {
                    T v = enumerator.Current;

                    if (v == null)
                    {
                        throw new NullReferenceException("One of the IEnumerator items is null");
                    }
                    item = v;
                    done = !enumerator.MoveNext();
                    return true;
                }
                item = default(T);
                return false;
            }

            public int RequestFusion(int mode)
            {
                return mode & FusionSupport.SYNC;
            }

            public void Request(long n)
            {
                if (n <= 0L)
                {
                    throw new ArgumentOutOfRangeException(nameof(n));
                }
                if (SubscriptionHelper.AddRequest(ref requested, n) == 0)
                {
                    OnRequest(n);
                }
            }

            protected abstract void OnRequest(long n);
        }

        internal sealed class EnumerableSubscription : AbstractEnumerableSubscription
        {
            readonly IFlowableSubscriber<T> actual;

            internal EnumerableSubscription(IFlowableSubscriber<T> actual, IEnumerator<T> enumerator) : base(enumerator)
            {
                this.actual = actual;
            }

            protected override void OnRequest(long n)
            {
                IFlowableSubscriber<T> a = actual;
                IEnumerator<T> en = enumerator;
                long e = 0L;

                for (;;)
                {
                    while (e != n)
                    {
                        if (Volatile.Read(ref cancelled) != 0)
                        {
                            Clear();
                            return;
                        }

                        T v = en.Current;
                        if (v == null)
                        {
                            Clear();
                            a.OnError(new NullReferenceException("One of the IEnumerator items is null"));
                            return;
                        }

                        a.OnNext(v);

                        bool b;

                        try
                        {
                            b = en.MoveNext();
                        }
                        catch (Exception ex)
                        {
                            Clear();
                            a.OnError(ex);
                            return;
                        }

                        if (!b)
                        {
                            Clear();
                            if (Volatile.Read(ref cancelled) == 0)
                            {
                                a.OnComplete();
                            }
                            return;
                        }

                        e++;
                    }

                    n = Volatile.Read(ref requested);
                    if (e == n)
                    {
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

        internal sealed class EnumerableConditionalSubscription : AbstractEnumerableSubscription
        {
            readonly IConditionalSubscriber<T> actual;

            internal EnumerableConditionalSubscription(IConditionalSubscriber<T> actual, IEnumerator<T> enumerator) : base(enumerator)
            {
                this.actual = actual;
            }

            protected override void OnRequest(long n)
            {
                IConditionalSubscriber<T> a = actual;
                IEnumerator<T> en = enumerator;
                long e = 0L;

                for (;;)
                {
                    while (e != n)
                    {
                        if (Volatile.Read(ref cancelled) != 0)
                        {
                            Clear();
                            return;
                        }

                        T v = en.Current;
                        if (v == null)
                        {
                            Clear();
                            a.OnError(new NullReferenceException("One of the IEnumerator items is null"));
                            return;
                        }

                        if (a.TryOnNext(v))
                        {
                            e++;
                        }

                        bool b;

                        try
                        {
                            b = en.MoveNext();
                        }
                        catch (Exception ex)
                        {
                            Clear();
                            a.OnError(ex);
                            return;
                        }

                        if (!b)
                        {
                            Clear();
                            if (Volatile.Read(ref cancelled) == 0)
                            {
                                a.OnComplete();
                            }
                            return;
                        }
                    }

                    n = Volatile.Read(ref requested);
                    if (e == n)
                    {
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
    }
}
