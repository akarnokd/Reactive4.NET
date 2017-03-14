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
    sealed class FlowableFromObservable<T> : AbstractFlowableSource<T>
    {
        readonly IObservable<T> source;

        readonly BackpressureStrategy strategy;

        internal FlowableFromObservable(IObservable<T> source, BackpressureStrategy strategy)
        {
            this.source = source;
            this.strategy = strategy;
        }

        public override void Subscribe(IFlowableSubscriber<T> subscriber)
        {
            switch (strategy)
            {
                case BackpressureStrategy.MISSING:
                    {
                        var parent = new FromObserverNone(subscriber);
                        subscriber.OnSubscribe(parent);
                        var d = source.Subscribe(parent);
                        parent.SetUpstream(d);
                    }
                    break;
                case BackpressureStrategy.ERROR:
                    {
                        var parent = new FromObserverError(subscriber);
                        subscriber.OnSubscribe(parent);
                        var d = source.Subscribe(parent);
                        parent.SetUpstream(d);
                    }
                    break;
                case BackpressureStrategy.DROP:
                    {
                        var parent = new FromObserverDrop(subscriber);
                        subscriber.OnSubscribe(parent);
                        var d = source.Subscribe(parent);
                        parent.SetUpstream(d);
                    }
                    break;
                case BackpressureStrategy.LATEST:
                    {
                        var parent = new FromObserverLatest(subscriber);
                        subscriber.OnSubscribe(parent);
                        var d = source.Subscribe(parent);
                        parent.SetUpstream(d);
                    }
                    break;
                default:
                    {
                        var parent = new FromObserverBuffer(subscriber);
                        subscriber.OnSubscribe(parent);
                        var d = source.Subscribe(parent);
                        parent.SetUpstream(d);
                    }
                    break;
            }
        }

        sealed class FromObserverNone : IObserver<T>, ISubscription
        {
            readonly IFlowableSubscriber<T> actual;

            IDisposable upstream;

            internal FromObserverNone(IFlowableSubscriber<T> actual)
            {
                this.actual = actual;
            }

            internal void SetUpstream(IDisposable d)
            {
                DisposableHelper.Replace(ref upstream, d);
            }

            public void Cancel()
            {
                DisposableHelper.Dispose(ref upstream);
            }

            public void OnCompleted()
            {
                upstream = DisposableHelper.Disposed;
                actual.OnComplete();
            }

            public void OnError(Exception error)
            {
                upstream = DisposableHelper.Disposed;
                actual.OnError(error);
            }

            public void OnNext(T value)
            {
                actual.OnNext(value);
            }

            public void Request(long n)
            {
                SubscriptionHelper.Validate(n);
            }
        }

        sealed class FromObserverError : IObserver<T>, ISubscription
        {
            readonly IFlowableSubscriber<T> actual;

            IDisposable upstream;

            long requested;

            long produced;

            internal FromObserverError(IFlowableSubscriber<T> actual)
            {
                this.actual = actual;
            }

            internal void SetUpstream(IDisposable d)
            {
                DisposableHelper.Replace(ref upstream, d);
            }

            public void Cancel()
            {
                DisposableHelper.Dispose(ref upstream);
            }

            public void OnCompleted()
            {
                if (upstream != DisposableHelper.Disposed)
                {
                    upstream = DisposableHelper.Disposed;
                    actual.OnComplete();
                }
            }

            public void OnError(Exception error)
            {
                if (upstream != DisposableHelper.Disposed)
                {
                    upstream = DisposableHelper.Disposed;
                    actual.OnError(error);
                }
            }

            public void OnNext(T value)
            {
                if (upstream != DisposableHelper.Disposed)
                {
                    long p = produced;
                    if (p != Volatile.Read(ref requested))
                    {
                        produced = p + 1;
                        actual.OnNext(value);
                    }
                    else
                    {
                        Cancel();
                        actual.OnError(new InvalidOperationException("Could not emit value due to lack of requests"));
                    }
                }
            }

            public void Request(long n)
            {
                if (SubscriptionHelper.Validate(n))
                {
                    SubscriptionHelper.AddRequest(ref requested, n);
                }
            }
        }

        sealed class FromObserverDrop : IObserver<T>, ISubscription
        {
            readonly IFlowableSubscriber<T> actual;

            IDisposable upstream;

            long requested;

            long produced;

            internal FromObserverDrop(IFlowableSubscriber<T> actual)
            {
                this.actual = actual;
            }

            internal void SetUpstream(IDisposable d)
            {
                DisposableHelper.Replace(ref upstream, d);
            }

            public void Cancel()
            {
                DisposableHelper.Dispose(ref upstream);
            }

            public void OnCompleted()
            {
                if (upstream != DisposableHelper.Disposed)
                {
                    upstream = DisposableHelper.Disposed;
                    actual.OnComplete();
                }
            }

            public void OnError(Exception error)
            {
                if (upstream != DisposableHelper.Disposed)
                {
                    upstream = DisposableHelper.Disposed;
                    actual.OnError(error);
                }
            }

            public void OnNext(T value)
            {
                if (upstream != DisposableHelper.Disposed)
                {
                    long p = produced;
                    if (p != Volatile.Read(ref requested))
                    {
                        produced = p + 1;
                        actual.OnNext(value);
                    }
                }
            }

            public void Request(long n)
            {
                if (SubscriptionHelper.Validate(n))
                {
                    SubscriptionHelper.AddRequest(ref requested, n);
                }
            }
        }

        sealed class FromObserverLatest : IObserver<T>, ISubscription
        {
            readonly IFlowableSubscriber<T> actual;

            IDisposable upstream;

            long requested;

            long emitted;

            int wip;

            bool done;
            Exception error;
            bool cancelled;

            object latest;

            internal FromObserverLatest(IFlowableSubscriber<T> actual)
            {
                this.actual = actual;
            }

            internal void SetUpstream(IDisposable d)
            {
                DisposableHelper.Replace(ref upstream, d);
            }

            public void Cancel()
            {
                Volatile.Write(ref cancelled, true);
                DisposableHelper.Dispose(ref upstream);
                if (Interlocked.Increment(ref wip) == 1)
                {
                    latest = null;
                }
            }

            public void OnCompleted()
            {
                Volatile.Write(ref done, true);
                Drain();
            }

            public void OnError(Exception error)
            {
                this.error = error;
                Volatile.Write(ref done, true);
                Drain();
            }

            public void OnNext(T value)
            {
                Interlocked.Exchange(ref latest, (object)value);
                Drain();
            }

            public void Request(long n)
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
                var e = emitted;

                for (;;)
                {
                    long r = Volatile.Read(ref requested);

                    while (e != r)
                    {
                        if (Volatile.Read(ref cancelled))
                        {
                            latest = null;
                            return;
                        }

                        bool d = Volatile.Read(ref done);
                        var o = Interlocked.Exchange(ref latest, null);
                        bool empty = o == null;
                        
                        if (d && empty)
                        {
                            var ex = error;
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

                        if (empty)
                        {
                            break;
                        }

                        a.OnNext((T)o);

                        e++;
                    }

                    if (e == r)
                    {
                        if (Volatile.Read(ref cancelled))
                        {
                            latest = null;
                            return;
                        }

                        if (Volatile.Read(ref done) && Volatile.Read(ref latest) == null)
                        {
                            var ex = error;
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

        sealed class FromObserverBuffer : IObserver<T>, ISubscription
        {
            readonly IFlowableSubscriber<T> actual;

            readonly ISimpleQueue<T> queue;

            IDisposable upstream;

            long requested;

            long emitted;

            int wip;

            bool done;
            Exception error;
            bool cancelled;

            internal FromObserverBuffer(IFlowableSubscriber<T> actual)
            {
                this.actual = actual;
                this.queue = new SpscLinkedArrayQueue<T>(Flowable.BufferSize());
            }

            internal void SetUpstream(IDisposable d)
            {
                DisposableHelper.Replace(ref upstream, d);
            }

            public void Cancel()
            {
                Volatile.Write(ref cancelled, true);
                DisposableHelper.Dispose(ref upstream);
                if (Interlocked.Increment(ref wip) == 1)
                {
                    queue.Clear();
                }
            }

            public void OnCompleted()
            {
                Volatile.Write(ref done, true);
                Drain();
            }

            public void OnError(Exception error)
            {
                this.error = error;
                Volatile.Write(ref done, true);
                Drain();
            }

            public void OnNext(T value)
            {
                queue.Offer(value);
                Drain();
            }

            public void Request(long n)
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
                var e = emitted;
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
                        bool empty = q.Poll(out T v);

                        if (d && empty)
                        {
                            var ex = error;
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

                        if (empty)
                        {
                            break;
                        }

                        a.OnNext((T)v);

                        e++;
                    }

                    if (e == r)
                    {
                        if (Volatile.Read(ref cancelled))
                        {
                            q.Clear();
                            return;
                        }

                        if (Volatile.Read(ref done) && q.IsEmpty())
                        {
                            var ex = error;
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
