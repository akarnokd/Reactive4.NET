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
    sealed class FlowableConcatMap<T, R> : AbstractFlowableOperator<T, R>
    {
        readonly Func<T, IPublisher<R>> mapper;

        readonly int prefetch;

        readonly ErrorMode errorMode;

        public FlowableConcatMap(IFlowable<T> source, Func<T, IPublisher<R>> mapper, int prefetch, ErrorMode errorMode) : base(source)
        {
            this.mapper = mapper;
            this.prefetch = prefetch;
            this.errorMode = errorMode;
        }

        public override void Subscribe(IFlowableSubscriber<R> subscriber)
        {
            if (errorMode == ErrorMode.Immediate)
            {
                source.Subscribe(new ConcatMapSubscriberEagerError(subscriber, mapper, prefetch));
            }
            else
            {
                source.Subscribe(new ConcatMapSubscriber(subscriber, mapper, prefetch, errorMode == ErrorMode.End));
            }
        }

        internal sealed class ConcatMapSubscriber : SubscriptionArbiter, IFlowableSubscriber<T>, ISubscription
        {
            readonly IFlowableSubscriber<R> actual;

            readonly Func<T, IPublisher<R>> mapper;

            readonly int prefetch;

            readonly int limit;

            readonly InnerSubscriber inner;

            readonly bool tillTheEnd;

            int wip;

            bool active;

            bool done;

            int consumed;

            ISubscription upstream;

            Exception error;

            ISimpleQueue<T> queue;

            int fusionMode;

            internal ConcatMapSubscriber(IFlowableSubscriber<R> actual, Func<T, IPublisher<R>> mapper, int prefetch, bool tillTheEnd)
            {
                this.actual = actual;
                this.mapper = mapper;
                this.prefetch = prefetch;
                this.limit = prefetch - (prefetch >> 2);
                this.inner = new InnerSubscriber(actual, this);
                this.tillTheEnd = tillTheEnd;
            }

            public override void Cancel()
            {
                base.Cancel();
                upstream.Cancel();
            }

            public void OnComplete()
            {
                if (!done)
                {
                    Volatile.Write(ref done, true);
                    Drain();
                }
            }

            public void OnError(Exception cause)
            {
                if (!done)
                {
                    ExceptionHelper.AddException(ref error, cause);
                    Volatile.Write(ref done, true);
                    Drain();
                }
            }

            public void OnNext(T element)
            {
                if (done)
                {
                    return;
                }
                
                if (fusionMode == FusionSupport.NONE)
                {
                    queue.Offer(element);
                }
                Drain();
            }

            public void OnSubscribe(ISubscription subscription)
            {
                if (SubscriptionHelper.Validate(ref upstream, subscription))
                {
                    if (subscription is IQueueSubscription<T> qs)
                    {
                        int m = qs.RequestFusion(FusionSupport.ANY);
                        if (m == FusionSupport.SYNC)
                        {
                            fusionMode = m;
                            queue = qs;
                            Volatile.Write(ref done, true);

                            actual.OnSubscribe(this);

                            Drain();
                            return;
                        }
                        if (m == FusionSupport.ASYNC)
                        {
                            fusionMode = m;
                            queue = qs;

                            actual.OnSubscribe(this);

                            qs.Request(prefetch);
                            return;
                        }
                    }

                    queue = new SpscArrayQueue<T>(prefetch);

                    actual.OnSubscribe(this);

                    upstream.Request(prefetch);
                }
            }

            void InnerComplete()
            {
                Volatile.Write(ref active, false);
                Drain();
            }

            void InnerError(Exception ex)
            {
                ExceptionHelper.AddException(ref error, ex);
                InnerComplete();
            }

            void Drain()
            {
                if (Interlocked.Increment(ref wip) != 1)
                {
                    return;
                }

                int missed = 1;
                var a = actual;
                var q = queue;
                int c = consumed;

                for (;;)
                {

                    if (ArbiterIsCancelled())
                    {
                        queue.Clear();
                        return;
                    }

                    if (!Volatile.Read(ref active))
                    {
                        if (!tillTheEnd && Volatile.Read(ref error) != null)
                        {
                            upstream.Cancel();
                            q.Clear();

                            Exception ex = ExceptionHelper.Terminate(ref error);
                            a.OnError(ex);
                            return;
                        }

                        bool d = Volatile.Read(ref done);
                        bool empty = !q.Poll(out T v);

                        if (d && empty)
                        {
                            Exception ex = ExceptionHelper.Terminate(ref error);
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

                        if (!empty)
                        {
                            if (fusionMode != FusionSupport.SYNC && ++c == limit)
                            {
                                c = 0;
                                upstream.Request(limit);
                            }

                            IPublisher<R> p;

                            try
                            {
                                p = mapper(v);
                                if (p == null)
                                {
                                    throw new NullReferenceException("The mapper returned a null IPublisher");
                                }
                            }
                            catch (Exception ex)
                            {
                                upstream.Cancel();
                                q.Clear();
                                ExceptionHelper.AddException(ref error, ex);
                                a.OnError(ExceptionHelper.Terminate(ref error));
                                return;
                            }

                            long e = inner.produced;
                            if (e != 0L)
                            {
                                inner.produced = 0L;
                                ArbiterProduced(e);
                            }

                            Volatile.Write(ref active, true);
                            p.Subscribe(inner);
                        }
                    }

                    int w = Volatile.Read(ref wip);
                    if (w == missed)
                    {
                        consumed = c;
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

            internal sealed class InnerSubscriber : IFlowableSubscriber<R>
            {
                readonly IFlowableSubscriber<R> actual;

                readonly ConcatMapSubscriber parent;

                internal long produced;

                internal InnerSubscriber(IFlowableSubscriber<R> actual, ConcatMapSubscriber parent)
                {
                    this.actual = actual;
                    this.parent = parent;
                }

                public void OnComplete()
                {
                    parent.InnerComplete();
                }

                public void OnError(Exception cause)
                {
                    parent.InnerError(cause);
                }

                public void OnNext(R element)
                {
                    produced++;
                    actual.OnNext(element);
                }

                public void OnSubscribe(ISubscription subscription)
                {
                    parent.ArbiterSet(subscription);
                }
            }
        }

        internal sealed class ConcatMapSubscriberEagerError : SubscriptionArbiter, IFlowableSubscriber<T>, ISubscription
        {
            readonly IFlowableSubscriber<R> actual;

            readonly Func<T, IPublisher<R>> mapper;

            readonly int prefetch;

            readonly int limit;

            readonly InnerSubscriber inner;

            int wip;

            bool active;

            bool done;

            int consumed;

            ISubscription upstream;

            Exception error;

            ISimpleQueue<T> queue;

            int fusionMode;

            int emitterWip;

            internal ConcatMapSubscriberEagerError(IFlowableSubscriber<R> actual, Func<T, IPublisher<R>> mapper, int prefetch)
            {
                this.actual = actual;
                this.mapper = mapper;
                this.prefetch = prefetch;
                this.limit = prefetch - (prefetch >> 2);
                this.inner = new InnerSubscriber(actual, this);
            }

            public override void Cancel()
            {
                base.Cancel();
                upstream.Cancel();
            }

            public void OnComplete()
            {
                if (!done)
                {
                    Volatile.Write(ref done, true);
                    Drain();
                }
            }

            public void OnError(Exception cause)
            {
                if (!done)
                {
                    base.Cancel();
                    SerializationHelper.OnError(actual, ref emitterWip, ref error, cause);
                    Volatile.Write(ref done, true);
                    Drain();
                }
            }

            public void OnNext(T element)
            {
                if (done)
                {
                    return;
                }

                if (fusionMode == FusionSupport.NONE)
                {
                    queue.Offer(element);
                }
                Drain();
            }

            public void OnSubscribe(ISubscription subscription)
            {
                if (SubscriptionHelper.Validate(ref upstream, subscription))
                {
                    if (subscription is IQueueSubscription<T> qs)
                    {
                        int m = qs.RequestFusion(FusionSupport.ANY);
                        if (m == FusionSupport.SYNC)
                        {
                            fusionMode = m;
                            queue = qs;
                            Volatile.Write(ref done, true);

                            actual.OnSubscribe(this);

                            Drain();
                            return;
                        }
                        if (m == FusionSupport.ASYNC)
                        {
                            fusionMode = m;
                            queue = qs;

                            actual.OnSubscribe(this);

                            qs.Request(prefetch);
                            return;
                        }
                    }

                    queue = new SpscArrayQueue<T>(prefetch);

                    actual.OnSubscribe(this);

                    upstream.Request(prefetch);
                }
            }
            void InnerNext(R value)
            {
                SerializationHelper.OnNext(actual, ref emitterWip, ref error, value);
            }

            void InnerComplete()
            {
                Volatile.Write(ref active, false);
                Drain();
            }

            void InnerError(Exception ex)
            {
                upstream.Cancel();
                base.Cancel();
                SerializationHelper.OnError(actual, ref emitterWip, ref error, ex);
                Drain();
            }

            void Drain()
            {
                if (Interlocked.Increment(ref wip) != 1)
                {
                    return;
                }

                int missed = 1;
                var a = actual;
                var q = queue;
                int c = consumed;

                for (; ; )
                {

                    if (ArbiterIsCancelled())
                    {
                        queue.Clear();
                        return;
                    }

                    if (!Volatile.Read(ref active))
                    {
                        bool d = Volatile.Read(ref done);
                        bool empty = !q.Poll(out T v);

                        if (d && empty)
                        {
                            a.OnComplete();
                            return;
                        }

                        if (!empty)
                        {
                            if (fusionMode != FusionSupport.SYNC && ++c == limit)
                            {
                                c = 0;
                                upstream.Request(limit);
                            }

                            IPublisher<R> p;

                            try
                            {
                                p = mapper(v);
                                if (p == null)
                                {
                                    throw new NullReferenceException("The mapper returned a null IPublisher");
                                }
                            }
                            catch (Exception ex)
                            {
                                upstream.Cancel();
                                q.Clear();
                                SerializationHelper.OnError(actual, ref emitterWip, ref error, ex);
                                return;
                            }

                            long e = inner.produced;
                            if (e != 0L)
                            {
                                inner.produced = 0L;
                                ArbiterProduced(e);
                            }

                            Volatile.Write(ref active, true);
                            p.Subscribe(inner);
                        }
                    }

                    int w = Volatile.Read(ref wip);
                    if (w == missed)
                    {
                        consumed = c;
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

            internal sealed class InnerSubscriber : IFlowableSubscriber<R>
            {
                readonly IFlowableSubscriber<R> actual;

                readonly ConcatMapSubscriberEagerError parent;

                internal long produced;

                internal InnerSubscriber(IFlowableSubscriber<R> actual, ConcatMapSubscriberEagerError parent)
                {
                    this.actual = actual;
                    this.parent = parent;
                }

                public void OnComplete()
                {
                    parent.InnerComplete();
                }

                public void OnError(Exception cause)
                {
                    parent.InnerError(cause);
                }

                public void OnNext(R element)
                {
                    produced++;
                    parent.InnerNext(element);
                }

                public void OnSubscribe(ISubscription subscription)
                {
                    parent.ArbiterSet(subscription);
                }
            }
        }
    }
}
