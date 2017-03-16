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
    sealed class FlowableZipArray<T, R> : AbstractFlowableSource<R>
    {
        readonly IPublisher<T>[] sources;

        readonly Func<T[], R> zipper;

        readonly int prefetch;

        internal FlowableZipArray(IPublisher<T>[] sources, Func<T[], R> zipper, int prefetch)
        {
            this.sources = sources;
            this.zipper = zipper;
            this.prefetch = prefetch;
        }

        public override void Subscribe(IFlowableSubscriber<R> subscriber)
        {
            var s = sources;
            var n = s.Length;
            var parent = new ZipSubscription(subscriber, zipper, n, prefetch);
            subscriber.OnSubscribe(parent);
            parent.Subscribe(s);
        }

        internal sealed class ZipSubscription : IQueueSubscription<R>
        {
            readonly IFlowableSubscriber<R> actual;

            readonly Func<T[], R> zipper;

            readonly int prefetch;

            readonly ZipSubscriber[] subscribers;

            Exception error;

            T[] current;

            bool[] hasValue;

            bool cancelled;

            int wip;

            long requested;

            long emitted;

            bool outputFused;

            internal ZipSubscription(IFlowableSubscriber<R> actual, Func<T[], R> zipper, int n, int prefetch)
            {
                this.actual = actual;
                this.zipper = zipper;
                this.prefetch = prefetch;
                var s = new ZipSubscriber[n];
                for (int i = 0; i < n; i++)
                {
                    s[i] = new ZipSubscriber(this, prefetch);
                }
                this.subscribers = s;
                this.current = new T[n];
                this.hasValue = new bool[n];
            }

            internal void Subscribe(IPublisher<T>[] sources)
            {
                var s = subscribers;
                var n = s.Length;

                for (int i = 0; i < n; i++)
                {
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
                foreach (var inner in subscribers)
                {
                    inner.Cancel();
                }
            }

            public void Clear()
            {
                current = null;
                hasValue = null;
            }

            public bool IsEmpty()
            {
                var s = subscribers;
                var n = s.Length;
                var hv = hasValue;
                int count = n;

                for (int i = 0; i < n; i++)
                {
                    var inner = subscribers[i];

                    var q = Volatile.Read(ref inner.queue);

                    if (q == null || q.IsEmpty() || !hv[i])
                    {
                        return true;
                    }
                }

                return false;
            }

            public bool Offer(R item)
            {
                throw new InvalidOperationException("Should not be called");
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

            void DrainAsync()
            {
                int missed = 1;
                var s = subscribers;
                var n = s.Length;
                var a = actual;
                long e = emitted;
                var curr = current;
                var hv = hasValue;

                for (;;) {
                    long r = Volatile.Read(ref requested);

                    while (e != r)
                    {
                        int count = n;

                        for (int i = 0; i < n; i++)
                        {
                            if (Volatile.Read(ref cancelled))
                            {
                                Clear();
                                return;
                            }

                            var inner = subscribers[i];

                            bool d = Volatile.Read(ref inner.done);

                            bool empty = false;

                            var q = Volatile.Read(ref inner.queue);
                            if (q == null)
                            {
                                empty = true;
                            }
                            else
                            if (!hv[i])
                            {
                                if (q.Poll(out T v))
                                {
                                    curr[i] = v;
                                    hv[i] = true;
                                }
                                else
                                {
                                    empty = true;
                                }
                            }

                            if (d && empty)
                            {
                                CancelAll();
                                Clear();
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
                                count--;
                            }
                        }

                        if (count == n)
                        {
                            R v;

                            var b = new T[n];
                            Array.Copy(curr, 0, b, 0, n);

                            try
                            {
                                v = zipper(b);
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

                            a.OnNext(v);

                            for (int i = 0; i < n; i++)
                            {
                                hv[i] = false;
                                s[i].RequestOne();
                            }

                            e++;
                        }
                        else
                        {
                            break;
                        }
                    }

                    if (e == r)
                    {
                        int count = n;

                        for (int i = 0; i < n; i++)
                        {
                            if (Volatile.Read(ref cancelled))
                            {
                                Clear();
                                return;
                            }

                            var inner = subscribers[i];

                            bool d = Volatile.Read(ref inner.done);

                            bool empty = false;

                            var q = Volatile.Read(ref inner.queue);
                            if (q == null)
                            {
                                empty = true;
                            }
                            else
                            if (!hv[i])
                            {
                                if (q.Poll(out T v))
                                {
                                    curr[i] = v;
                                    hv[i] = true;
                                }
                                else
                                {
                                    empty = true;
                                }
                            }

                            if (d && empty)
                            {
                                CancelAll();
                                Clear();
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
                                count--;
                            }
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

            public bool Poll(out R item)
            {
                var s = subscribers;
                var n = s.Length;
                var curr = current;
                var hv = hasValue;
                int count = n;

                for (int i = 0; i < n; i++)
                {
                    var inner = subscribers[i];

                    bool d = Volatile.Read(ref inner.done);

                    bool empty = false;

                    var q = Volatile.Read(ref inner.queue);
                    if (q == null)
                    {
                        empty = true;
                    }
                    else
                    if (!hv[i])
                    {
                        if (q.Poll(out T v))
                        {
                            curr[i] = v;
                            hv[i] = true;
                        }
                        else
                        {
                            empty = true;
                        }
                    }

                    if (d && empty)
                    {
                        CancelAll();
                        Clear();
                        var ex = ExceptionHelper.Terminate(ref error);
                        if (ex != null)
                        {
                            actual.OnError(ex);
                        }
                        else
                        {
                            actual.OnComplete();
                        }
                        item = default(R);
                        return false;
                    }

                    if (empty)
                    {
                        count--;
                    }
                }

                if (count == n)
                {
                    var b = new T[n];
                    Array.Copy(curr, 0, b, 0, n);
                    item = zipper(b);

                    for (int i = 0; i < n; i++)
                    {
                        hv[i] = false;
                        s[i].RequestOne();
                    }
                    return true;
                }
                item = default(R);
                return false;
            }

            void DrainFused()
            {
                int missed = 1;
                var s = subscribers;
                var n = s.Length;
                var a = actual;
                var curr = current;
                var hv = hasValue;

                for (;;)
                {
                    int count = n;

                    for (int i = 0; i < n; i++)
                    {
                        if (Volatile.Read(ref cancelled))
                        {
                            Clear();
                            return;
                        }

                        var inner = subscribers[i];

                        bool d = Volatile.Read(ref inner.done);

                        bool empty = false;

                        var q = Volatile.Read(ref inner.queue);
                        if (q == null)
                        {
                            empty = true;
                        }
                        else
                        if (!hv[i])
                        {
                            if (q.Poll(out T v))
                            {
                                curr[i] = v;
                                hv[i] = true;
                            }
                            else
                            {
                                empty = true;
                            }
                        }

                        if (d && empty)
                        {
                            CancelAll();
                            Clear();
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
                            count--;
                        }
                    }

                    if (count == n)
                    {
                        a.OnNext(default(R));
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

            void InnerError(ZipSubscriber inner, Exception cause)
            {
                if (!inner.done)
                {
                    ExceptionHelper.AddException(ref error, cause);
                    Volatile.Write(ref inner.done, true);
                    Drain();
                }
            }

            void InnerComplete(ZipSubscriber inner)
            {
                if (!inner.done)
                {
                    Volatile.Write(ref inner.done, true);
                    Drain();
                }
            }

            sealed class ZipSubscriber : IFlowableSubscriber<T>
            {
                readonly ZipSubscription parent;

                readonly int prefetch;

                readonly int limit;

                ISubscription upstream;

                internal ISimpleQueue<T> queue;

                int fusionMode;

                internal bool done;

                int produced;

                internal ZipSubscriber(ZipSubscription parent, int prefetch)
                {
                    this.parent = parent;
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
                    if (fusionMode == FusionSupport.NONE)
                    {
                        queue.Offer(element);
                    }
                    parent.Drain();
                }

                public void OnSubscribe(ISubscription subscription)
                {
                    if (SubscriptionHelper.SetOnce(ref upstream, subscription))
                    {
                        if (subscription is IQueueSubscription<T> qs)
                        {
                            int m = qs.RequestFusion(FusionSupport.ANY | FusionSupport.BARRIER);
                            if (m == FusionSupport.SYNC)
                            {
                                fusionMode = m;
                                Volatile.Write(ref queue, qs);
                                Volatile.Write(ref done, true);
                                parent.Drain();
                                return;
                            }

                            if (m == FusionSupport.ASYNC)
                            {
                                fusionMode = m;
                                Volatile.Write(ref queue, qs);

                                subscription.Request(prefetch);
                                return;
                            }
                        }

                        Volatile.Write(ref queue, new SpscArrayQueue<T>(prefetch));

                        subscription.Request(prefetch);
                    }
                }

                internal void Cancel()
                {
                    SubscriptionHelper.Cancel(ref upstream);
                }

                internal void RequestOne()
                {
                    if (fusionMode != FusionSupport.NONE)
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
}
