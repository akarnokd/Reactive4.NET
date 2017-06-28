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
    sealed class FlowableCreate<T> : AbstractFlowableSource<T>
    {
        readonly Action<IFlowableEmitter<T>> emitter;

        readonly BackpressureStrategy strategy;

        internal FlowableCreate(Action<IFlowableEmitter<T>> emitter, BackpressureStrategy strategy)
        {
            this.emitter = emitter;
            this.strategy = strategy;
        }

        public override void Subscribe(IFlowableSubscriber<T> subscriber)
        {
            AbstractFlowableEmitter parent;
            switch (strategy)
            {
                case BackpressureStrategy.MISSING:
                    parent = new MissingFlowableEmitter(subscriber);
                    break;
                case BackpressureStrategy.ERROR:
                    parent = new ErrorFlowableEmitter(subscriber);
                    break;
                case BackpressureStrategy.DROP:
                    parent = new DropFlowableEmitter(subscriber);
                    break;
                case BackpressureStrategy.LATEST:
                    parent = new LatestFlowableEmitter(subscriber);
                    break;
                default:
                    parent = new BufferFlowableEmitter(subscriber, Flowable.BufferSize());
                    break;
            }
            subscriber.OnSubscribe(parent);
            try
            {
                emitter(parent);   
            }
            catch (Exception ex)
            {
                parent.OnError(ex);
            }
        }

        abstract class AbstractFlowableEmitter : ISubscription, IFlowableEmitter<T>
        {
            public bool IsCancelled => DisposableHelper.IsDisposed(ref disposable);

            IDisposable disposable;

            internal IFlowableSubscriber<T> actual;

            public long Requested => Volatile.Read(ref requested);

            internal long requested;

            internal AbstractFlowableEmitter(IFlowableSubscriber<T> actual)
            {
                this.actual = actual;
            }

            public virtual void Cancel()
            {
                DisposableHelper.Dispose(ref disposable);
            }

            public void OnCancel(Action action)
            {
                OnCancel(new ActionDisposable(action));   
            }

            public void OnCancel(IDisposable disposable)
            {
                DisposableHelper.Set(ref this.disposable, disposable);
            }

            public abstract void Request(long n);

            public abstract void OnNext(T item);

            public abstract void OnError(Exception error);

            public abstract void OnComplete();
        }

        sealed class MissingFlowableEmitter : AbstractFlowableEmitter
        {
            public MissingFlowableEmitter(IFlowableSubscriber<T> actual) : base(actual)
            {
            }

            bool done;

            public override void OnComplete()
            {
                if (!done && !IsCancelled)
                {
                    done = true;
                    actual.OnComplete();
                    Cancel();
                }
            }

            public override void OnError(Exception error)
            {
                if (!done && !IsCancelled)
                {
                    done = true;
                    actual.OnError(error);
                    Cancel();
                }
            }

            public override void OnNext(T item)
            {
                if (!done && !IsCancelled)
                {
                    actual.OnNext(item);
                }
            }

            public override void Request(long n)
            {
                if (SubscriptionHelper.Validate(n))
                {
                    SubscriptionHelper.AddRequest(ref requested, n);
                }
            }
        }

        abstract class NoOverflowFlowableEmitter : AbstractFlowableEmitter
        {
            public NoOverflowFlowableEmitter(IFlowableSubscriber<T> actual) : base(actual)
            {
            }

            bool done;

            long emitted;

            public override void OnComplete()
            {
                if (!done && !IsCancelled)
                {
                    done = true;
                    actual.OnComplete();
                    Cancel();
                }
            }

            public override void OnError(Exception error)
            {
                if (!done && !IsCancelled)
                {
                    done = true;
                    actual.OnError(error);
                    Cancel();
                }
            }

            public override void OnNext(T item)
            {
                if (!done && !IsCancelled)
                {
                    long e = emitted;
                    if (e != Requested)
                    {
                        emitted = e + 1;
                        actual.OnNext(item);
                    }
                    else
                    {
                        OnOverflow();
                    }
                }
            }

            internal abstract void OnOverflow();

            public override void Request(long n)
            {
                if (SubscriptionHelper.Validate(n))
                {
                    SubscriptionHelper.AddRequest(ref requested, n);
                }
            }
        }

        sealed class ErrorFlowableEmitter : NoOverflowFlowableEmitter
        {
            public ErrorFlowableEmitter(IFlowableSubscriber<T> actual) : base(actual)
            {
            }

            internal override void OnOverflow()
            {
                OnError(new InvalidOperationException("Could not emit value due to lack of requests"));
            }
        }

        sealed class DropFlowableEmitter : NoOverflowFlowableEmitter
        {
            public DropFlowableEmitter(IFlowableSubscriber<T> actual) : base(actual)
            {
            }

            internal override void OnOverflow()
            {
                // ignored
            }
        }

        sealed class LatestFlowableEmitter : AbstractFlowableEmitter
        {
            public LatestFlowableEmitter(IFlowableSubscriber<T> actual) : base(actual)
            {
            }

            bool done;
            Exception error;
            long emitted;

            object latest;

            int wip;

            public override void OnComplete()
            {
                if (!done && !IsCancelled)
                {
                    Volatile.Write(ref done, true);
                    Drain();
                }
            }

            public override void OnError(Exception error)
            {
                if (!done && !IsCancelled)
                {
                    this.error = error;
                    Volatile.Write(ref done, true);
                    Drain();
                }
            }

            public override void OnNext(T item)
            {
                if (!done && !IsCancelled)
                {
                    Interlocked.Exchange(ref latest, item);
                    Drain();
                }
            }

            public override void Request(long n)
            {
                if (SubscriptionHelper.Validate(n))
                {
                    SubscriptionHelper.AddRequest(ref requested, n);
                    Drain();
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
                long e = emitted;

                for (;;)
                {
                    long r = Requested;
                    while (e != r)
                    {
                        if (IsCancelled)
                        {
                            latest = null;
                            return;
                        }
                        bool d = Volatile.Read(ref done);
                        object v = Interlocked.Exchange(ref latest, null);
                        bool empty = v == null;

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

                        a.OnNext((T)v);

                        e++;
                    }

                    if (e == r)
                    {
                        if (IsCancelled)
                        {
                            latest = null;
                            return;
                        }

                        if (Volatile.Read(ref done) && Volatile.Read(ref latest) == null)
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
        }

        sealed class BufferFlowableEmitter : AbstractFlowableEmitter, IQueueSubscription<T>
        {
            readonly ISimpleQueue<T> queue;

            bool done;
            Exception error;

            long emitted;

            int wip;

            bool outputFused;

            public BufferFlowableEmitter(IFlowableSubscriber<T> actual, int capacityHint) : base(actual)
            {
                queue = new SpscLinkedArrayQueue<T>(capacityHint);
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

            public override void OnComplete()
            {
                if (!done && !IsCancelled)
                {
                    Volatile.Write(ref done, true);
                    Drain();
                }
            }

            public override void OnError(Exception error)
            {
                if (!done && !IsCancelled)
                {
                    this.error = error;
                    Volatile.Write(ref done, true);
                    Drain();
                }
            }

            public override void OnNext(T item)
            {
                if (!done && !IsCancelled)
                {
                    queue.Offer(item);
                    Drain();
                }
            }

            public bool Poll(out T item)
            {
                return queue.Poll(out item);
            }

            public override void Request(long n)
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
                var a = actual;
                long e = emitted;
                var q = queue;

                for (;;)
                {
                    long r = Requested;
                    while (e != r)
                    {
                        if (IsCancelled)
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
                        if (IsCancelled)
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

            void DrainFused()
            {
                int missed = 1;
                var a = actual;
                long e = emitted;
                var q = queue;

                for (;;)
                {
                    if (IsCancelled)
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

                    if (d)
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
