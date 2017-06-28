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
    internal sealed class FlowableFlatMap<T, R> : AbstractFlowableOperator<T, R>
    {
        readonly Func<T, IPublisher<R>> mapper;

        readonly int maxConcurrency;

        readonly int bufferSize;

        public FlowableFlatMap(IFlowable<T> source, 
            Func<T, IPublisher<R>> mapper,
            int maxConcurrency,
            int bufferSize) : base(source)
        {
            this.mapper = mapper;
            this.maxConcurrency = maxConcurrency;
            this.bufferSize = bufferSize;
        }

        public override void Subscribe(IFlowableSubscriber<R> subscriber)
        {
            source.Subscribe(new FlatMapMainSubscriber(subscriber, mapper, maxConcurrency, bufferSize));
        }

        internal sealed class FlatMapMainSubscriber : IFlowableSubscriber<T>, ISubscription
        {
            readonly IFlowableSubscriber<R> actual;

            readonly Func<T, IPublisher<R>> mapper;

            readonly int maxConcurrency;

            readonly int bufferSize;

            ISubscription upstream;

            int cancelled;
            bool done;
            Exception error;

            long requested;

            long emitted;

            int wip;

            FlatMapInnerSubscriber[] subscribers;

            static readonly FlatMapInnerSubscriber[] EMPTY = new FlatMapInnerSubscriber[0];
            static readonly FlatMapInnerSubscriber[] TERMINATED = new FlatMapInnerSubscriber[0];

            internal FlatMapMainSubscriber(IFlowableSubscriber<R> actual, Func<T, IPublisher<R>> mapper,
                int maxConcurrency, int bufferSize)
            {
                this.actual = actual;
                this.mapper = mapper;
                this.maxConcurrency = maxConcurrency;
                this.bufferSize = bufferSize;
                Interlocked.Exchange(ref subscribers, EMPTY);
            }

            public void Cancel()
            {
                if (Interlocked.Exchange(ref cancelled, 1) == 0)
                {
                    upstream.Cancel();
                    CancelAll();
                }
            }

            void CancelAll()
            {
                foreach (FlatMapInnerSubscriber inner in Interlocked.Exchange(ref subscribers, TERMINATED))
                {
                    inner.Cancel();
                }
            }

            public void OnComplete()
            {
                if (!Volatile.Read(ref done))
                {
                    Volatile.Write(ref done, true);
                    Drain();
                }
            }

            public void OnError(Exception cause)
            {
                if (!Volatile.Read(ref done))
                {
                    ExceptionHelper.AddException(ref error, cause);
                    Volatile.Write(ref done, true);
                    Drain();
                }
            }

            public void OnNext(T element)
            {
                if (Volatile.Read(ref done))
                {
                    return;
                }
                IPublisher<R> p;

                try
                {
                    p = mapper(element);
                    if (p == null)
                    {
                        throw new NullReferenceException("The mapper returned a null IPublisher");
                    }
                }
                catch (Exception ex)
                {
                    upstream.Cancel();
                    OnError(ex);
                    return;
                }

                FlatMapInnerSubscriber inner = new FlatMapInnerSubscriber(this, bufferSize);
                if (Add(inner))
                {
                    p.Subscribe(inner);
                }
            }

            bool Add(FlatMapInnerSubscriber inner)
            {
                for (;;)
                {
                    var a = Volatile.Read(ref subscribers);
                    if (a == TERMINATED)
                    {
                        return false;
                    }
                    int n = a.Length;
                    var b = new FlatMapInnerSubscriber[n + 1];
                    Array.Copy(a, 0, b, 0, n);
                    b[n] = inner;
                    if (Interlocked.CompareExchange(ref subscribers, b, a) == a)
                    {
                        return true;
                    }
                }
            }

            void Remove(FlatMapInnerSubscriber inner)
            {
                for (;;)
                {
                    var a = Volatile.Read(ref subscribers);
                    int n = a.Length;
                    if (n == 0)
                    {
                        break;
                    }
                    int j = -1;
                    for (int i = 0; i < n; i++)
                    {
                        if (a[i] == inner)
                        {
                            j = i;
                            break;
                        }
                    }
                    if (j < 0)
                    {
                        break;
                    }
                    FlatMapInnerSubscriber[] b;
                    if (n == 1)
                    {
                        b = EMPTY;
                    }
                    else
                    {
                        b = new FlatMapInnerSubscriber[n - 1];
                        Array.Copy(a, 0, b, 0, j);
                        Array.Copy(a, j + 1, b, j, n - j - 1);
                    }
                    if (Interlocked.CompareExchange(ref subscribers, b, a) == a)
                    {
                        break;
                    }
                }
            }

            public void OnSubscribe(ISubscription subscription)
            {
                if (SubscriptionHelper.SetOnce(ref upstream, subscription))
                {
                    actual.OnSubscribe(this);

                    subscription.Request(maxConcurrency);
                }
            }

            public void Request(long n)
            {
                if (n <= 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(n));
                }
                SubscriptionHelper.AddRequest(ref requested, n);
                Drain();
            }

            void Drain()
            {
                if (Interlocked.Increment(ref wip) == 1)
                {
                    DrainLoop();
                }
            }

            void DrainLoop()
            {
                IFlowableSubscriber<R> a = actual;
                int missed = 1;
                long e = emitted;

                for (;;)
                {
                    if (Volatile.Read(ref cancelled) != 0)
                    {
                        return;
                    }
                    bool again = false;
                    bool d = Volatile.Read(ref done);
                    FlatMapInnerSubscriber[] s = Volatile.Read(ref subscribers);
                    int n = s.Length;

                    if (d && n == 0)
                    {
                        Exception ex = ExceptionHelper.Terminate(ref error);
                        if (ex == null)
                        {
                            a.OnComplete();
                        }
                        else
                        {
                            a.OnError(ex);
                        }
                        return;
                    }

                    long r = Volatile.Read(ref requested);

                    if (n != 0)
                    {

                        for (int i = 0; i < n; i++)
                        {
                            if (Volatile.Read(ref cancelled) != 0)
                            {
                                return;
                            }
                            if (r == e)
                            {
                                break;
                            }

                            var inner = s[i];

                            bool innerDone = Volatile.Read(ref inner.done);
                            ISimpleQueue<R> q = Volatile.Read(ref inner.queue);
                            bool innerEmpty = q == null || q.IsEmpty();

                            if (innerDone && innerEmpty)
                            {
                                Remove(inner);
                                upstream.Request(1);
                                again = true;
                            }
                            else
                            if (!innerEmpty)
                            {
                                while (e != r)
                                {
                                    if (Volatile.Read(ref cancelled) != 0)
                                    {
                                        return;
                                    }
                                    innerDone = Volatile.Read(ref inner.done);
                                    innerEmpty = !q.Poll(out R item);
                                    
                                    if (innerDone && innerEmpty)
                                    {
                                        Remove(inner);
                                        upstream.Request(1);
                                        again = true;
                                        break;
                                    }

                                    if (innerEmpty)
                                    {
                                        break;
                                    }

                                    a.OnNext(item);

                                    e++;
                                    inner.Request(1);
                                }

                                if (e == r)
                                {
                                    if (Volatile.Read(ref cancelled) != 0)
                                    {
                                        return;
                                    }
                                    innerDone = Volatile.Read(ref inner.done);
                                    innerEmpty = q.IsEmpty();
                                    if (innerDone && innerEmpty)
                                    {
                                        Remove(inner);
                                        upstream.Request(1);
                                        again = true;
                                    }
                                }
                            }
                        }
                    }
                    
                    if (e == r)
                    {
                        d = Volatile.Read(ref done);
                        s = Volatile.Read(ref subscribers);
                        n = s.Length;

                        if (d && n == 0)
                        {
                            Exception ex = ExceptionHelper.Terminate(ref error);
                            if (ex == null)
                            {
                                a.OnComplete();
                            }
                            else
                            {
                                a.OnError(ex);
                            }
                            return;
                        }
                    }

                    if (again)
                    {
                        continue;
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

            internal void InnerNext(FlatMapInnerSubscriber inner, R item)
            {
                if (Volatile.Read(ref wip) == 0 && Interlocked.CompareExchange(ref wip, 1, 0) == 0)
                {
                    long e = emitted;
                    if (Volatile.Read(ref requested) != e)
                    {
                        actual.OnNext(item);
                        emitted = e + 1;
                        inner.Request(1);
                    }
                    else
                    {
                        ISimpleQueue<R> q = inner.GetOrCreateQueue();
                        q.Offer(item);
                    }
                    if (Interlocked.Decrement(ref wip) == 0)
                    {
                        return;
                    }
                } else
                {
                    ISimpleQueue<R> q = inner.GetOrCreateQueue();
                    q.Offer(item);
                    if (Interlocked.Increment(ref wip) != 1)
                    {
                        return;
                    }
                }

                DrainLoop();
            }

            internal void InnerError(FlatMapInnerSubscriber inner, Exception ex)
            {
                ExceptionHelper.AddException(ref error, ex);
                Volatile.Write(ref inner.done, true);
                Drain();
            }

            internal void InnerComplete(FlatMapInnerSubscriber inner)
            {
                Volatile.Write(ref inner.done, true);
                Drain();
            }

            internal sealed class FlatMapInnerSubscriber : IFlowableSubscriber<R>, ISubscription
            {
                readonly FlatMapMainSubscriber parent;

                readonly int bufferSize;

                readonly int limit;

                long produced;

                int fusionMode;

                ISubscription upstream;

                internal bool done;

                internal ISimpleQueue<R> queue;

                internal FlatMapInnerSubscriber(FlatMapMainSubscriber parent, int bufferSize)
                {
                    this.parent = parent;
                    this.bufferSize = bufferSize;
                    this.limit = bufferSize - (bufferSize >> 2);
                }

                public void Cancel()
                {
                    SubscriptionHelper.Cancel(ref upstream);
                }

                public void OnComplete()
                {
                    parent.InnerComplete(this);
                }

                public void OnError(Exception cause)
                {
                    parent.InnerError(this, cause);
                }

                public void OnNext(R element)
                {
                    if (fusionMode != FusionSupport.ASYNC)
                    {
                        parent.InnerNext(this, element);
                    }
                    else
                    {
                        parent.Drain();
                    }
                }

                public void OnSubscribe(ISubscription subscription)
                {
                    if (SubscriptionHelper.SetOnce(ref upstream, subscription))
                    {
                        if (subscription is IQueueSubscription<R> qs)
                        {
                            int mode = qs.RequestFusion(FusionSupport.ANY | FusionSupport.BARRIER);
                            if (mode == FusionSupport.SYNC)
                            {
                                fusionMode = mode;
                                Volatile.Write(ref queue, qs);
                                Volatile.Write(ref done, true);
                                parent.Drain();
                                return;
                            }
                            if (mode == FusionSupport.ASYNC)
                            {
                                fusionMode = mode;
                                Volatile.Write(ref queue, qs);
                                subscription.Request(bufferSize);

                                return;
                            }
                        }
                        subscription.Request(bufferSize);
                    }
                }

                public void Request(long n)
                {
                    if (fusionMode != FusionSupport.SYNC)
                    {
                        long p = produced + n;
                        if (p >= limit)
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

                internal ISimpleQueue<R> GetOrCreateQueue()
                {
                    ISimpleQueue<R> q = Volatile.Read(ref queue);
                    if (q == null)
                    {
                        q = new SpscArrayQueue<R>(bufferSize);
                        Volatile.Write(ref queue, q);
                    }
                    return q;
                }
            }
        }
    }

    internal sealed class FlowableFlatMapPublisher<T, R> : AbstractFlowableSource<R>
    {
        readonly IPublisher<T> source;

        readonly Func<T, IPublisher<R>> mapper;

        readonly int maxConcurrency;

        readonly int bufferSize;

        public FlowableFlatMapPublisher(IPublisher<T> source,
            Func<T, IPublisher<R>> mapper,
            int maxConcurrency,
            int bufferSize)
        {
            this.source = source;
            this.mapper = mapper;
            this.maxConcurrency = maxConcurrency;
            this.bufferSize = bufferSize;
        }

        public override void Subscribe(IFlowableSubscriber<R> subscriber)
        {
            source.Subscribe(new FlowableFlatMap<T, R>.FlatMapMainSubscriber(subscriber, mapper, maxConcurrency, bufferSize));
        }
    }
}
