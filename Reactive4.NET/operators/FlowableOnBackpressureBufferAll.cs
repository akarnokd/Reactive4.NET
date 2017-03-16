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
    sealed class FlowableOnBackpressureBufferAll<T> : AbstractFlowableOperator<T, T>
    {
        readonly int capacityHint;

        public FlowableOnBackpressureBufferAll(IFlowable<T> source, int capacityHint) : base(source)
        {
            this.capacityHint = capacityHint;
        }

        public override void Subscribe(IFlowableSubscriber<T> subscriber)
        {
            if (subscriber is IConditionalSubscriber<T> s)
            {
                source.Subscribe(new OnBackpressureBufferAllConditionalSubscriber(s, capacityHint));
            }
            else
            {
                source.Subscribe(new OnBackpressureBufferAllSubscriber(subscriber, capacityHint));
            }
        }

        abstract class AbstractOnBackpressureBufferAll : IFlowableSubscriber<T>, IQueueSubscription<T>
        {
            internal readonly ISimpleQueue<T> queue;

            internal ISubscription upstream;

            internal bool cancelled;
            internal bool done;
            internal Exception error;

            internal long requested;

            internal long emitted;

            internal int wip;

            internal bool outputFused;

            public AbstractOnBackpressureBufferAll(int capacityHint)
            {
                queue = new SpscLinkedArrayQueue<T>(capacityHint);
            }

            public void Cancel()
            {
                Volatile.Write(ref cancelled, true);
                upstream.Cancel();
            }

            public void Clear()
            {
                queue.Clear();
            }

            public bool IsEmpty()
            {
                return queue.IsEmpty();
            }

            public bool Offer(T item)
            {
                throw new InvalidOperationException("Should not be called");
            }

            public void OnComplete()
            {
                if (!done)
                {
                    Volatile.Write(ref done, true);
                    Drain();
                }
            }

            public void OnError(Exception error)
            {
                if (!done)
                {
                    this.error = error;
                    Volatile.Write(ref done, true);
                    Drain();
                }
            }

            public void OnNext(T item)
            {
                queue.Offer(item);
                Drain();
            }

            public bool Poll(out T item)
            {
                return queue.Poll(out item);
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

            public void OnSubscribe(ISubscription subscription)
            {
                if (SubscriptionHelper.Validate(ref upstream, subscription))
                {
                    OnStart();

                    subscription.Request(long.MaxValue);
                }
            }

            internal abstract void OnStart();

            internal abstract void DrainFused();

            internal abstract void DrainAsync();
        }

        sealed class OnBackpressureBufferAllSubscriber : AbstractOnBackpressureBufferAll
        {
            readonly IFlowableSubscriber<T> actual;

            public OnBackpressureBufferAllSubscriber(IFlowableSubscriber<T> actual, int capacityHint) : base(capacityHint)
            {
                this.actual = actual;
            }

            internal override void OnStart()
            {
                actual.OnSubscribe(this);
            }

            internal override void DrainAsync()
            {
                int missed = 1;
                var a = actual;
                long e = emitted;
                var q = queue;

                for (;;)
                {
                    long r = Volatile.Read(ref requested);
                    while (e != r)
                    {
                        if (Volatile.Read(ref cancelled))
                        {
                            q.Clear();
                            return;
                        }
                        bool d = Volatile.Read(ref done);
                        bool empty = !q.Poll(out T v);

                        if (d && empty)
                        {
                            var ex = error;
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

                        a.OnNext(v);

                        e++;
                    }

                    if (e == r)
                    {
                        if (Volatile.Read(ref cancelled))
                        {

                            return;
                        }

                        if (Volatile.Read(ref done) && q.IsEmpty())
                        {
                            var ex = error;
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

            internal override void DrainFused()
            {
                int missed = 1;
                var a = actual;
                long e = emitted;
                var q = queue;

                for (;;)
                {
                    if (Volatile.Read(ref cancelled))
                    {
                        q.Clear();
                        return;
                    }
                    bool d = Volatile.Read(ref done);
                    bool empty = q.IsEmpty();

                    if (!empty)
                    {
                        a.OnNext(default(T));
                    }

                    if (d && empty)
                    {
                        var ex = error;
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
        }

        sealed class OnBackpressureBufferAllConditionalSubscriber : AbstractOnBackpressureBufferAll
        {
            readonly IConditionalSubscriber<T> actual;

            public OnBackpressureBufferAllConditionalSubscriber(IConditionalSubscriber<T> actual, int capacityHint) : base(capacityHint)
            {
                this.actual = actual;
            }

            internal override void OnStart()
            {
                actual.OnSubscribe(this);
            }

            internal override void DrainAsync()
            {
                int missed = 1;
                var a = actual;
                long e = emitted;
                var q = queue;

                for (;;)
                {
                    long r = Volatile.Read(ref requested);
                    while (e != r)
                    {
                        if (Volatile.Read(ref cancelled))
                        {
                            q.Clear();
                            return;
                        }
                        bool d = Volatile.Read(ref done);
                        bool empty = !q.Poll(out T v);

                        if (d && empty)
                        {
                            var ex = error;
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

                        if (a.TryOnNext(v))
                        {
                            e++;
                        }
                    }

                    if (e == r)
                    {
                        if (Volatile.Read(ref cancelled))
                        {

                            return;
                        }

                        if (Volatile.Read(ref done) && q.IsEmpty())
                        {
                            var ex = error;
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

            internal override void DrainFused()
            {
                int missed = 1;
                var a = actual;
                long e = emitted;
                var q = queue;

                for (;;)
                {
                    if (Volatile.Read(ref cancelled))
                    {
                        q.Clear();
                        return;
                    }
                    bool d = Volatile.Read(ref done);
                    bool empty = q.IsEmpty();

                    if (!empty)
                    {
                        a.TryOnNext(default(T));
                    }

                    if (d && empty)
                    {
                        var ex = error;
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
        }
    }
}
