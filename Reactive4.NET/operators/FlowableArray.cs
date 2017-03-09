using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Reactive4.NET.operators
{
    internal sealed class FlowableArray<T> : AbstractFlowableSource<T>
    {
        readonly T[] array;

        internal FlowableArray(T[] array)
        {
            this.array = array;
        }

        public override void Subscribe(IFlowableSubscriber<T> subscriber)
        {
            if (subscriber is IConditionalSubscriber<T> s)
            {
                subscriber.OnSubscribe(new ArrayConditionalSubscription(s, array));
            }
            else
            {
                subscriber.OnSubscribe(new ArraySubscription(subscriber, array));
            }
        }

        internal sealed class ArraySubscription : IQueueSubscription<T>
        {
            readonly IFlowableSubscriber<T> actual;

            readonly T[] array;

            int index;

            long requested;

            bool cancelled;

            internal ArraySubscription(IFlowableSubscriber<T> actual, T[] array)
            {
                this.actual = actual;
                this.array = array;
            }

            public void Cancel()
            {
                Volatile.Write(ref cancelled, true);
            }

            public void Clear()
            {
                index = array.Length;
            }

            public bool IsEmpty()
            {
                return index == array.Length;
            }

            public bool Offer(T item)
            {
                throw new InvalidOperationException("Should not be called!");
            }

            public bool Poll(out T item)
            {
                int idx = index;
                T[] a = array;
                if (idx == a.Length)
                {
                    item = default(T);
                    return false;
                }
                T v = a[idx];
                if (v == null)
                {
                    throw new NullReferenceException("An array item was null");
                }
                item = v;
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
                    T[] array = this.array;
                    int f = array.Length;
                    long e = 0;
                    IFlowableSubscriber<T> a = actual;

                    for (;;)
                    {

                        while (idx != f && e != n)
                        {
                            if (Volatile.Read(ref cancelled))
                            {
                                return;
                            }

                            T v = array[idx];

                            if (v == null)
                            {
                                a.OnError(new NullReferenceException("An array item was null"));
                                return;
                            }

                            a.OnNext(v);

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

        internal sealed class ArrayConditionalSubscription : IQueueSubscription<T>
        {
            readonly IConditionalSubscriber<T> actual;

            readonly T[] array;

            int index;

            long requested;

            bool cancelled;

            internal ArrayConditionalSubscription(IConditionalSubscriber<T> actual, T[] array)
            {
                this.actual = actual;
                this.array = array;
            }

            public void Cancel()
            {
                Volatile.Write(ref cancelled, true);
            }

            public void Clear()
            {
                index = array.Length;
            }

            public bool IsEmpty()
            {
                return index == array.Length;
            }

            public bool Offer(T item)
            {
                throw new InvalidOperationException("Should not be called!");
            }

            public bool Poll(out T item)
            {
                int idx = index;
                T[] a = array;
                if (idx == a.Length)
                {
                    item = default(T);
                    return false;
                }
                T v = a[idx];
                if (v == null)
                {
                    throw new NullReferenceException("An array item was null");
                }
                item = v;
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
                    T[] array = this.array;
                    int f = array.Length;
                    long e = 0;
                    IConditionalSubscriber<T> a = actual;

                    for (;;)
                    {

                        while (idx != f && e != n)
                        {
                            if (Volatile.Read(ref cancelled))
                            {
                                return;
                            }

                            T v = array[idx];

                            if (v == null)
                            {
                                a.OnError(new NullReferenceException("An array item was null"));
                                return;
                            }

                            if (a.TryOnNext(v))
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
