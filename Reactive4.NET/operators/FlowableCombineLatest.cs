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
    sealed class FlowableCombineLatest<T, R> : AbstractFlowableSource<R>
    {
        readonly IPublisher<T>[] sources;

        readonly Func<T[], R> combiner;

        readonly int prefetch;

        internal FlowableCombineLatest(IPublisher<T>[] sources, Func<T[], R> combiner, int prefetch)
        {
            this.sources = sources;
            this.combiner = combiner;
            this.prefetch = prefetch;
        }

        public override void Subscribe(IFlowableSubscriber<R> subscriber)
        {
            var s = sources;
            var n = s.Length;
            var parent = new CombineLatestSubscription(subscriber, combiner, n, prefetch);
            subscriber.OnSubscribe(parent);

            parent.Subscribe(s);
        }

        internal sealed class CombineLatestSubscription : IQueueSubscription<R>
        {
            readonly IFlowableSubscriber<R> actual;

            readonly CombineLatestSubscriber[] subscribers;

            readonly ISimpleQueue<Entry> queue;

            readonly Func<T[], R> combiner;

            bool cancelled;
            int active;
            int terminated;

            Exception error;

            long requested;

            long emitted;

            int wip;

            bool outputFused;

            T[] latest;

            internal CombineLatestSubscription(IFlowableSubscriber<R> actual, Func<T[], R> combiner, int n, int prefetch)
            {
                this.actual = actual;
                this.combiner = combiner;
                latest = new T[n];
                var s = new CombineLatestSubscriber[n];
                for (int i = 0; i < n; i++)
                {
                    s[i] = new CombineLatestSubscriber(this, i, prefetch);
                }
                this.subscribers = s;
                this.queue = new SpscLinkedArrayQueue<Entry>(prefetch);
            }

            internal void Subscribe(IPublisher<T>[] sources)
            {
                var s = subscribers;
                var n = s.Length;

                for (int i = 0; i < n; i++)
                {
                    if (Volatile.Read(ref cancelled))
                    {
                        return;
                    }
                    sources[i].Subscribe(s[i]);
                }
            }

            public void Cancel()
            {
                Volatile.Write(ref cancelled, true);
                CancelAll();
                if (Interlocked.Increment(ref wip) == 1)
                {
                    Clear();
                }
            }

            void CancelAll()
            {
                foreach (var s in subscribers)
                {
                    s.Cancel();
                }
            }

            public void Clear()
            {
                queue.Clear();
                ClearLatest();
            }

            void ClearLatest()
            {
                int n = subscribers.Length;
                for (int i = 0; i < n; i++)
                {
                    latest[i] = default(T);
                }
            }

            public bool IsEmpty()
            {
                return queue.IsEmpty();
            }

            public bool Offer(R item)
            {
                throw new InvalidOperationException("Should not be called");
            }

            public bool Poll(out R item)
            {
                if (queue.Poll(out Entry e))
                {
                    item = combiner(e.items);
                    e.sender.RequestOne();
                    return true;
                }
                item = default(R);
                return false;
            }

            public void Request(long n)
            {
                if (SubscriptionHelper.Validate(n))
                {
                    SubscriptionHelper.AddRequest(ref requested, n);
                    Drain();
                }
            }

            public int RequestFusion(int mode)
            {
                if ((mode & FusionSupport.ASYNC) != 0)
                {
                    outputFused = true;
                    return FusionSupport.ASYNC;
                }
                return FusionSupport.NONE;
            }

            void InnerNext(CombineLatestSubscriber sender, T item)
            {
                bool drain = false;
                lock (this)
                {
                    if (!sender.hasValue)
                    {
                        active++;
                        sender.hasValue = true;
                    }

                    latest[sender.index] = item;

                    int n = subscribers.Length;
                    drain = active == n;
                    if (drain)
                    {
                        Entry e = new Entry();
                        e.sender = sender;
                        e.items = new T[n];
                        Array.Copy(latest, 0, e.items, 0, n);
                        queue.Offer(e);
                    }
                }

                if (drain)
                {
                    Drain();
                }
                else
                {
                    sender.RequestOne();
                }
            }

            void InnerError(CombineLatestSubscriber sender, Exception cause)
            {
                if (!sender.done)
                {
                    sender.done = true;
                    ExceptionHelper.AddException(ref error, cause);
                    Interlocked.Increment(ref terminated);
                    Drain();
                }
            }

            void InnerComplete(CombineLatestSubscriber sender)
            {
                if (!sender.done)
                {
                    sender.done = true;
                    Interlocked.Increment(ref terminated);
                    Drain();
                }
            }

            void Drain()
            {
                if (Interlocked.Increment(ref wip) != 1)
                {
                    return;
                }

                if (outputFused)
                {
                    DrainFused();
                }
                else
                {
                    DrainAsync();
                }
            }

            void DrainFused()
            {
                int missed = 1;
                var n = subscribers.Length;
                var q = queue;
                var a = actual;

                for (;;)
                {
                    bool d = Volatile.Read(ref terminated) == n;
                    bool empty = q.IsEmpty();

                    if (!empty)
                    {
                        a.OnNext(default(R));
                    }

                    if (d)
                    {
                        ClearLatest();
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
            }

            void DrainAsync()
            {

                int missed = 1;
                var q = queue;
                var a = actual;
                var n = subscribers.Length;
                var e = emitted;

                for (;;)
                {
                    long r = Volatile.Read(ref requested);

                    while (e != r)
                    {
                        if (Volatile.Read(ref cancelled))
                        {
                            Clear();
                            return;
                        }

                        bool d = Volatile.Read(ref terminated) == n;
                        bool empty = !q.Poll(out Entry v);

                        if (d && empty)
                        {
                            ClearLatest();
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

                        if (empty)
                        {
                            break;
                        }

                        R t;

                        try
                        {
                            t = combiner(v.items);
                        }
                        catch (Exception ex)
                        {
                            CancelAll();
                            Clear();
                            ExceptionHelper.AddException(ref error, ex);
                            ex = ExceptionHelper.Terminate(ref error);
                            a.OnError(ex);
                            return;
                        }

                        a.OnNext(t);

                        e++;
                    }

                    if (e == r)
                    {
                        if (Volatile.Read(ref cancelled))
                        {
                            Clear();
                            return;
                        }

                        bool d = Volatile.Read(ref terminated) == n;
                        bool empty = q.IsEmpty();

                        if (d && empty)
                        {
                            ClearLatest();
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

            internal struct Entry
            {
                internal CombineLatestSubscriber sender;
                internal T[] items;
            }

            internal sealed class CombineLatestSubscriber : IFlowableSubscriber<T>
            {
                readonly CombineLatestSubscription parent;

                internal readonly int index;

                readonly int prefetch;

                readonly int limit;

                ISubscription upstream;

                int produced;

                internal bool hasValue;

                internal bool done;

                internal CombineLatestSubscriber(CombineLatestSubscription parent, int index, int prefetch)
                {
                    this.parent = parent;
                    this.index = index;
                    this.prefetch = prefetch;
                    this.limit = prefetch - (prefetch >> 2);
                }

                public void OnComplete()
                {
                    parent.InnerComplete(this);
                }

                public void OnError(Exception cause)
                {
                    parent.InnerError(this, cause);
                }

                public void OnNext(T element)
                {
                    parent.InnerNext(this, element);
                }

                public void OnSubscribe(ISubscription subscription)
                {
                    if (SubscriptionHelper.SetOnce(ref upstream, subscription))
                    {
                        upstream.Request(prefetch);
                    }
                }

                internal void Cancel()
                {
                    SubscriptionHelper.Cancel(ref upstream);
                }

                internal void RequestOne()
                {
                    int p = produced + 1;
                    if (p == limit)
                    {
                        produced = 0;
                        upstream.Request(p);
                    }
                    else
                    {
                        produced = p;
                    }
                }
            }
        }
    }
}
