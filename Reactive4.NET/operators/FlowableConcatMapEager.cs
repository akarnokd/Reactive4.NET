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
    class FlowableConcatMapEager<T, R> : AbstractFlowableOperator<T, R>
    {
        readonly Func<T, IPublisher<R>> mapper;

        readonly int maxConcurrency;

        readonly int prefetch;

        public FlowableConcatMapEager(IFlowable<T> source, Func<T, IPublisher<R>> mapper,
            int maxConcurrency, int prefetch) : base(source)
        {
            this.mapper = mapper;
            this.maxConcurrency = maxConcurrency;
            this.prefetch = prefetch;
        }

        public override void Subscribe(IFlowableSubscriber<R> subscriber)
        {
            source.Subscribe(new ConcatMapEagerSubscriber(subscriber, mapper, maxConcurrency, prefetch));
        }

        internal sealed class ConcatMapEagerSubscriber : IFlowableSubscriber<T>, IQueueSubscription<R>
        {
            readonly IFlowableSubscriber<R> actual;

            readonly Func<T, IPublisher<R>> mapper;

            readonly int maxConcurrency;

            readonly int prefetch;

            readonly InnerSubscriber[] subscribers;

            int producerIndex;

            int consumerIndex;

            static readonly InnerSubscriber CancelledInner = new InnerSubscriber(null, 0);

            ISubscription upstream;

            long requested;

            long emitted;

            bool done;
            Exception error;

            bool cancelled;

            InnerSubscriber current;

            int wip;

            bool outputFused;
            
            internal ConcatMapEagerSubscriber(IFlowableSubscriber<R> actual,
                Func<T, IPublisher<R>> mapper, int maxConcurrency, int prefetch)
            {
                this.actual = actual;
                this.mapper = mapper;
                this.maxConcurrency = maxConcurrency;
                this.prefetch = prefetch;
                this.subscribers = new InnerSubscriber[maxConcurrency];
            }

            bool Add(InnerSubscriber inner)
            {
                var s = subscribers;
                var n = s.Length;
                int pi = producerIndex;
                if (Interlocked.CompareExchange(ref s[pi], inner, null) == null)
                {
                    pi++;
                    producerIndex = pi == n ? 0 : pi;
                    return true;
                }
                return false;
            }

            bool Remove(InnerSubscriber inner)
            {
                var s = subscribers;
                var n = s.Length;
                int ci = consumerIndex;
                if (Interlocked.CompareExchange(ref s[ci], null, inner) == inner) {
                    ci++;
                    consumerIndex = ci == n ? 0 : ci;
                    return true;
                }
                return false;
            }

            void CancelAll()
            {
                var s = subscribers;
                int n = s.Length;
                for (int i = 0; i < n; i++)
                {
                    Interlocked.Exchange(ref s[i], CancelledInner)?.Cancel();
                }
            }

            public void Cancel()
            {
                upstream.Cancel();
                CancelAll();
                if (Interlocked.Increment(ref wip) != 1)
                {
                    current = null;
                }
            }

            public void OnComplete()
            {
                if (done)
                {
                    return;
                }
                Volatile.Write(ref done, true);
                Drain();
            }

            public void OnError(Exception cause)
            {
                if (done)
                {
                    return;
                }
                ExceptionHelper.AddException(ref error, cause);
                Volatile.Write(ref done, true);
                Drain();
            }

            public void OnNext(T element)
            {
                if (done)
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
                } catch (Exception ex)
                {
                    upstream.Cancel();
                    OnError(ex);
                    return;
                }

                InnerSubscriber inner = new InnerSubscriber(this, prefetch);
                if (Add(inner))
                {
                    p.Subscribe(inner);
                }
            }

            public void OnSubscribe(ISubscription subscription)
            {
                if (SubscriptionHelper.Validate(ref upstream, subscription))
                {
                    actual.OnSubscribe(this);

                    subscription.Request(maxConcurrency);
                }
            }

            public void Request(long n)
            {
                if (SubscriptionHelper.Validate(n))
                {
                    SubscriptionHelper.AddRequest(ref requested, n);
                    Drain();
                }
            }

            void InnerNext(InnerSubscriber inner, R item)
            {
                if (Volatile.Read(ref wip) == 0 && Interlocked.CompareExchange(ref wip, 1, 0) == 0)
                {
                    var q = Volatile.Read(ref inner.queue);
                    bool offer = true;
                    if (current == inner)
                    {
                        if (q == null || q.IsEmpty())
                        {
                            var e = emitted;
                            if (e != Volatile.Read(ref requested))
                            {
                                actual.OnNext(item);
                                emitted = e + 1;
                                inner.RequestOne();
                                offer = false;
                            }
                        }
                    }
                    if (offer)
                    {
                        if (q == null)
                        {
                            q = inner.GetOrCreateQueue();
                        }
                        q.Offer(item);
                    }
                    if (Interlocked.Decrement(ref wip) == 0)
                    {
                        return;
                    }
                }
                else
                {
                    var q = inner.GetOrCreateQueue();
                    q.Offer(item);
                    if (Interlocked.Increment(ref wip) != 1)
                    {
                        return;
                    }
                }
                DrainLoop();
            }

            void InnerError(InnerSubscriber inner, Exception error)
            {
                ExceptionHelper.AddException(ref this.error, error);
                Volatile.Write(ref inner.done, true);
                Drain();
            }

            void InnerComplete(InnerSubscriber inner)
            {
                Volatile.Write(ref inner.done, true);
                Drain();
            }

            void Drain()
            {
                if (Interlocked.Increment(ref wip) != 1)
                {
                    return;
                }
                DrainLoop();
            }

            void DrainLoop()
            {
                if (outputFused)
                {
                    DrainLoopFused();
                }
                else
                {
                    DrainLoopAsync();
                }
            }

            void DrainLoopFused()
            {
                int missed = 1;
                var s = subscribers;
                var n = s.Length;
                var curr = current;
                var a = actual;

                for (;;)
                {
                    if (Volatile.Read(ref cancelled))
                    {
                        current = null;
                        return;
                    }

                    bool d = Volatile.Read(ref done);

                    int ci = consumerIndex;
                    curr = Volatile.Read(ref s[ci]);

                    if (curr != null)
                    {
                        a.OnNext(default(R));
                    }

                    if (d)
                    {
                        var ex = ExceptionHelper.Terminate(ref error);
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

            void DrainLoopAsync()
            {
                int missed = 1;
                var s = subscribers;
                var n = s.Length;
                var curr = current;
                var e = emitted;
                var a = actual;

                for (;;)
                {
                    if (curr == null)
                    {
                        if (Volatile.Read(ref cancelled))
                        {
                            return;
                        }

                        bool d = Volatile.Read(ref done);

                        int ci = consumerIndex;
                        curr = Volatile.Read(ref s[ci]);

                        if (d && curr == null)
                        {
                            current = null;
                            var ex = ExceptionHelper.Terminate(ref error);
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
                        if (curr == CancelledInner)
                        {
                            current = null;
                            return;
                        }
                    }

                    if (curr != null)
                    {
                        long r = Volatile.Read(ref requested);

                        if (Volatile.Read(ref cancelled))
                        {
                            current = null;
                            return;
                        }

                        bool d = Volatile.Read(ref curr.done);
                        var q = Volatile.Read(ref curr.queue);

                        if (d && q == null)
                        {
                            Remove(curr);
                            curr = null;
                        }
                        else
                        if (q != null)
                        {
                            while (e != r)
                            {
                                if (Volatile.Read(ref cancelled))
                                {
                                    current = null;
                                    return;
                                }

                                d = Volatile.Read(ref curr.done);

                                bool empty = !q.Poll(out R v);

                                if (d && empty)
                                {
                                    Remove(curr);
                                    curr = null;
                                    break;
                                }

                                if (empty)
                                {
                                    break;
                                }

                                a.OnNext(v);

                                e++;
                                curr.RequestOne();
                            }

                            if (e == r)
                            {
                                if (Volatile.Read(ref cancelled))
                                {
                                    current = null;
                                    return;
                                }
                                if (Volatile.Read(ref curr.done) && q.IsEmpty())
                                {
                                    Remove(curr);
                                    curr = null;
                                }
                            }
                        }

                        if (curr == null)
                        {
                            upstream.Request(1);
                            continue;
                        }
                    }
                    int w = Volatile.Read(ref wip);
                    if (w == missed)
                    {
                        emitted = e;
                        current = curr;
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

            public int RequestFusion(int mode)
            {
                if ((mode & FusionSupport.ASYNC) != 0)
                {
                    outputFused = true;
                    return FusionSupport.ASYNC;
                }
                return FusionSupport.NONE;
            }

            public bool Offer(R item)
            {
                throw new InvalidOperationException("Should not be called");
            }

            public bool Poll(out R item)
            {
                var curr = current;
                var s = subscribers;
                var n = s.Length;
                for (;;)
                {
                    if (curr == null)
                    {
                        int ci = consumerIndex;
                        curr = Volatile.Read(ref s[ci]);
                        if (curr == null)
                        {
                            item = default(R);
                            return false;
                        }
                        current = curr;
                    }
                    if (curr != null)
                    {
                        bool d = Volatile.Read(ref curr.done);
                        var q = Volatile.Read(ref curr.queue);
                        if (q != null && q.Poll(out item))
                        {
                            return true;
                        }
                        if (d)
                        {
                            Remove(curr);
                            int ci = consumerIndex + 1;
                            consumerIndex = ci == n ? 0 : ci;
                            current = null;
                            curr = null;
                            upstream.Request(1);
                            continue;
                        }
                    }
                    item = default(R);
                    return false;
                }
            }

            public bool IsEmpty()
            {
                var curr = current;
                if (curr != null)
                {
                    var q = Volatile.Read(ref curr.queue);
                    return q == null || q.IsEmpty();
                }
                return true;
            }

            public void Clear()
            {
                current = null;
                var s = subscribers;
                var n = s.Length;
                int ci = consumerIndex;
                for (;;)
                {
                    var inner = Volatile.Read(ref s[ci]);
                    if (inner == null || inner == CancelledInner)
                    {
                        break;
                    }
                    if (Interlocked.CompareExchange(ref s[ci], null, inner) == inner)
                    {
                        ci++;
                        if (ci == n)
                        {
                            ci = 0;
                        }
                    }
                }
                consumerIndex = ci;
            }

            sealed class InnerSubscriber : IFlowableSubscriber<R>
            {
                readonly ConcatMapEagerSubscriber parent;

                readonly int prefetch;

                readonly int limit;

                ISubscription upstream;

                internal ISimpleQueue<R> queue;

                internal bool done;

                int fusionMode;

                int produced;

                internal InnerSubscriber(ConcatMapEagerSubscriber parent, int prefetch)
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

                public void OnNext(R element)
                {
                    if (fusionMode == FusionSupport.NONE)
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

                        int pf = prefetch;
                        if (pf == int.MaxValue)
                        {
                            subscription.Request(long.MaxValue);
                        }
                        else
                        {
                            subscription.Request(pf);
                        }
                    }
                }

                internal ISimpleQueue<R> GetOrCreateQueue()
                {
                    var q = Volatile.Read(ref queue);
                    if (q == null)
                    {
                        int pf = prefetch;
                        if (pf == int.MaxValue)
                        {
                            q = new SpscLinkedArrayQueue<R>(Flowable.BufferSize());
                        }
                        else
                        {
                            q = new SpscArrayQueue<R>(pf);
                        }
                        Volatile.Write(ref queue, q);
                    }
                    return q;
                }

                internal void Cancel()
                {
                    SubscriptionHelper.Cancel(ref upstream);
                }

                internal void RequestOne()
                {
                    if (fusionMode != FusionSupport.SYNC)
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

    class FlowableConcatMapEagerPublisher<T, R> : AbstractFlowableSource<R>
    {
        readonly IPublisher<T> source;
        
        readonly Func<T, IPublisher<R>> mapper;

        readonly int maxConcurrency;

        readonly int prefetch;

        public FlowableConcatMapEagerPublisher(IPublisher<T> source, Func<T, IPublisher<R>> mapper,
            int maxConcurrency, int prefetch)
        {
            this.source = source;
            this.mapper = mapper;
            this.maxConcurrency = maxConcurrency;
            this.prefetch = prefetch;
        }

        public override void Subscribe(IFlowableSubscriber<R> subscriber)
        {
            source.Subscribe(new FlowableConcatMapEager<T, R>.ConcatMapEagerSubscriber(subscriber, mapper, maxConcurrency, prefetch));
        }
    }
}
