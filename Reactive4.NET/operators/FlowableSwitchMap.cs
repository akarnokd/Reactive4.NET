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
    sealed class FlowableSwitchMap<T, R> : AbstractFlowableOperator<T, R>
    {
        readonly Func<T, IPublisher<R>> mapper;

        readonly int prefetch;

        public FlowableSwitchMap(IFlowable<T> source,
            Func<T, IPublisher<R>> mapper, int prefetch) : base(source)
        {
            this.mapper = mapper;
            this.prefetch = prefetch;
        }

        public override void Subscribe(IFlowableSubscriber<R> subscriber)
        {
            source.Subscribe(new SwitchMapSubscriber(subscriber, mapper, prefetch));
        }

        internal sealed class SwitchMapSubscriber : IFlowableSubscriber<T>, ISubscription
        {
            readonly IFlowableSubscriber<R> actual;

            readonly Func<T, IPublisher<R>> mapper;

            readonly int prefetch;

            ISubscription upstream;

            bool done;
            Exception error;
            bool cancelled;

            long requested;

            long emitted;

            int wip;

            InnerSubscriber current;

            static readonly InnerSubscriber Terminated = new InnerSubscriber(null, 0);

            internal SwitchMapSubscriber(IFlowableSubscriber<R> actual, 
                Func<T, IPublisher<R>> mapper, int prefetch)
            {
                this.actual = actual;
                this.mapper = mapper;
                this.prefetch = prefetch;
            }

            public void Cancel()
            {
                upstream.Cancel();
                Interlocked.Exchange(ref current, Terminated)?.Cancel();
            }

            public void OnComplete()
            {
                Volatile.Write(ref done, true);
                Drain();
            }

            public void OnError(Exception cause)
            {
                ExceptionHelper.AddException(ref error, cause);
                Volatile.Write(ref done, true);
                Drain();
            }

            public void OnNext(T element)
            {
                var curr = Volatile.Read(ref current);
                if (curr != Terminated)
                {
                    curr?.Cancel();

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

                    var inner = new InnerSubscriber(this, prefetch);

                    for (;;)
                    {
                        curr = Volatile.Read(ref current);
                        if (curr == Terminated)
                        {
                            break;
                        }
                        if (Interlocked.CompareExchange(ref current, inner, curr) == curr)
                        {
                            p.Subscribe(inner);
                            break;
                        }
                    }
                }
            }

            public void OnSubscribe(ISubscription subscription)
            {
                if (SubscriptionHelper.Validate(ref upstream, subscription))
                {
                    actual.OnSubscribe(this);

                    subscription.Request(long.MaxValue);
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
                    if (Volatile.Read(ref current) == inner)
                    {
                        var q = Volatile.Read(ref inner.queue);
                        bool offer = true;
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
                        if (offer)
                        {
                            if (q == null)
                            {
                                q = inner.GetOrCreateQueue();
                            }
                            q.Offer(item);
                        }
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

            void InnerError(InnerSubscriber inner, Exception cause)
            {
                ExceptionHelper.AddException(ref error, cause);
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
                int missed = 1;
                var e = emitted;
                var a = actual;
                
                for (;;)
                {
                    if (Volatile.Read(ref cancelled))
                    {
                        current = null;
                        return;
                    }
                    bool d = Volatile.Read(ref done);
                    var curr = Volatile.Read(ref current);

                    if (d && curr == null)
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

                    if (curr != null)
                    {
                        d = Volatile.Read(ref curr.done);
                        var q = Volatile.Read(ref curr.queue);

                        if (q != null)
                        {
                            if (e != Volatile.Read(ref requested))
                            {
                                bool empty = !q.Poll(out R v);
                                if (d && empty)
                                {
                                    Interlocked.CompareExchange(ref current, null, curr);
                                    continue;
                                }
                                if (!empty)
                                {
                                    a.OnNext(v);
                                    e++;
                                    curr.RequestOne();
                                    continue;
                                }
                            }
                            else
                            {
                                if (d && q.IsEmpty())
                                {
                                    Interlocked.CompareExchange(ref current, null, curr);
                                    continue;
                                }
                            }
                        }
                        else
                        if (d)
                        {
                            Interlocked.CompareExchange(ref current, null, curr);
                            continue;
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

            sealed class InnerSubscriber : IFlowableSubscriber<R>
            {
                readonly SwitchMapSubscriber parent;

                readonly int prefetch;

                readonly int limit;

                ISubscription upstream;

                internal ISimpleQueue<R> queue;

                internal bool done;

                int fusionMode;

                int produced;

                internal InnerSubscriber(SwitchMapSubscriber parent, int prefetch)
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

    sealed class FlowableSwitchMapPublisher<T, R> : AbstractFlowableSource<R>
    {
        readonly IPublisher<T> source;

        readonly Func<T, IPublisher<R>> mapper;

        readonly int prefetch;

        public FlowableSwitchMapPublisher(IPublisher<T> source,
            Func<T, IPublisher<R>> mapper, int prefetch)
        {
            this.source = source;
            this.mapper = mapper;
            this.prefetch = prefetch;
        }

        public override void Subscribe(IFlowableSubscriber<R> subscriber)
        {
            source.Subscribe(new FlowableSwitchMap<T, R>.SwitchMapSubscriber(subscriber, mapper, prefetch));
        }
    }
}
