using Reactive.Streams;
using Reactive4.NET.utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Reactive4.NET.operators
{
    sealed class ParallelFlowableSorted<T> : AbstractFlowableSource<T>
    {
        readonly IParallelFlowable<IList<T>> source;

        readonly IComparer<T> comparer;

        internal ParallelFlowableSorted(IParallelFlowable<IList<T>> source, IComparer<T> comparer)
        {
            this.source = source;
            this.comparer = comparer;
        }

        public override void Subscribe(IFlowableSubscriber<T> subscriber)
        {
            var src = source;
            int n = src.Parallelism;

            var parent = new SortedSubscription(subscriber, n, comparer);
            subscriber.OnSubscribe(parent);
            source.Subscribe(parent.subscribers);
        }

        sealed class SortedSubscription : ISubscription
        {
            readonly IFlowableSubscriber<T> actual;

            internal readonly SortedSubscriber[] subscribers;

            readonly Entry[] items;

            readonly IComparer<T> comparer;

            static readonly IList<T> EmptyList = new List<T>();

            bool cancelled;
            int done;

            Exception error;

            int wip;

            long requested;

            long emitted;

            internal SortedSubscription(IFlowableSubscriber<T> actual, int n, IComparer<T> comparer)
            {
                this.actual = actual;
                var subs = new SortedSubscriber[n];
                for (int i = 0; i < n; i++)
                {
                    subs[i] = new SortedSubscriber(this, i);
                }
                this.items = new Entry[n];
                this.subscribers = subs;
                this.done = n;
                this.comparer = comparer;
            }

            public void Cancel()
            {
                Volatile.Write(ref cancelled, true);
                CancelAll();
                if (Interlocked.Increment(ref wip) == 1)
                {
                    var list = items;
                    Clear(list, list.Length);
                }
            }

            void Clear(Entry[] list, int n)
            {
                for (int i = 0; i < n; i++)
                {
                    list[i].list = null;
                    subscribers[i] = null;
                }
            }

            void CancelAll()
            {
                foreach (var inner in subscribers)
                {
                    inner?.Cancel();
                }
            }

            public void Request(long n)
            {
                if (SubscriptionHelper.Validate(n))
                {
                    SubscriptionHelper.AddRequest(ref requested, n);
                    if (Volatile.Read(ref done) == 0)
                    {
                        Drain();
                    }
                }
            }

            void Drain()
            {
                if (Interlocked.Increment(ref wip) != 1)
                {
                    return;
                }

                int missed = 1;
                var a = actual;
                var arr = items;
                int n = arr.Length;
                long e = emitted;
                var cmp = comparer;

                for (;;)
                {
                    long r = Volatile.Read(ref requested);

                    while (e != r)
                    {
                        if (Volatile.Read(ref cancelled))
                        {
                            Clear(arr, n);
                            return;
                        }

                        T min = default(T);
                        bool hasMin = false;
                        int minIndex = -1;

                        bool empty = true;

                        for (int i = 0; i < n; i++)
                        {
                            int idx = arr[i].index;
                            if (idx != arr[i].n)
                            {
                                T v = arr[i].list[idx];
                                if (!hasMin)
                                {
                                    min = v;
                                    minIndex = i;
                                    hasMin = true;
                                }
                                else
                                {
                                    if (cmp.Compare(min, v) > 0)
                                    {
                                        min = v;
                                        minIndex = i;
                                    }
                                }
                                empty = false;
                            }
                        }

                        if (empty)
                        {
                            Clear(arr, n);
                            var ex = ExceptionHelper.Terminate(ref error);
                            if (ex != null)
                            {
                                a.OnError(ex);
                            }
                            else
                            {
                                a.OnComplete();
                            }

                            return;
                        }

                        a.OnNext(min);

                        e++;
                        arr[minIndex].index++;
                    }

                    if (e == r)
                    {
                        if (Volatile.Read(ref cancelled))
                        {
                            Clear(arr, n);
                            return;
                        }

                        bool empty = true;

                        for (int i = 0; i < n; i++)
                        {
                            if (arr[i].index != arr[i].n)
                            {
                                empty = false;
                                break;
                            }
                        }

                        if (empty)
                        {
                            Clear(arr, n);
                            var ex = ExceptionHelper.Terminate(ref error);
                            if (ex != null)
                            {
                                a.OnError(ex);
                            }
                            else
                            {
                                a.OnComplete();
                            }

                            return;
                        }
                    }

                    int w = Volatile.Read(ref wip);
                    if (w == missed)
                    {
                        emitted = e;
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
            }

            void InnerDone(IList<T> list, int index)
            {
                this.items[index].list = list;
                this.items[index].n = list.Count;
                if (Interlocked.Decrement(ref done) == 0)
                {
                    Drain();
                }
            }

            void InnerError(Exception cause, int index)
            {
                ExceptionHelper.AddException(ref error, cause);
                this.items[index].list = EmptyList;
                if (Interlocked.Decrement(ref done) == 0)
                {
                    Drain();
                }
            }

            internal struct Entry
            {
                internal IList<T> list;
                internal int n;
                internal int index;
            }

            internal sealed class SortedSubscriber : IFlowableSubscriber<IList<T>>
            {
                readonly SortedSubscription parent;

                readonly int index;

                ISubscription upstream;

                bool done;

                internal SortedSubscriber(SortedSubscription parent, int index)
                {
                    this.parent = parent;
                    this.index = index;
                }

                public void OnComplete()
                {
                    if (!done)
                    {
                        parent.InnerDone(EmptyList, index);
                    }
                }

                public void OnError(Exception cause)
                {
                    parent.InnerError(cause, index);
                }

                public void OnNext(IList<T> element)
                {
                    done = true;
                    parent.InnerDone(element, index);
                }

                public void OnSubscribe(ISubscription subscription)
                {
                    if (SubscriptionHelper.SetOnce(ref upstream, subscription))
                    {
                        subscription.Request(long.MaxValue);
                    }
                }

                internal void Cancel()
                {
                    SubscriptionHelper.Cancel(ref upstream);
                }
            }
        }
    }
}
