using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Reactive.Streams;
using Reactive4.NET.operators;
using System.Threading;

namespace Reactive4.NET
{
    public sealed class DirectProcessor<T> : IFlowableProcessor<T>, IDisposable
    {
        public bool HasComplete {
            get
            {
                return Volatile.Read(ref subscribers) == Terminated && error == null;
            }
        }

        public bool HasException
        {
            get
            {
                return Volatile.Read(ref subscribers) == Terminated && error != null;
            }
        }

        public Exception Exception
        {
            get
            {
                return Volatile.Read(ref subscribers) == Terminated ? error : null;
            }
        }

        public bool HasSubscribers
        {
            get
            {
                return Volatile.Read(ref subscribers).Length != 0;
            }
        }

        ProcessorSubscription[] subscribers = Empty;

        static readonly ProcessorSubscription[] Empty = new ProcessorSubscription[0];
        static readonly ProcessorSubscription[] Terminated = new ProcessorSubscription[0];

        Exception error;
        int once;

        ISubscription upstream;

        public void Dispose()
        {
            SubscriptionHelper.Cancel(ref upstream);
        }

        public void OnComplete()
        {
            if (Interlocked.CompareExchange(ref once, 1, 0) == 0)
            {
                foreach (var inner in Interlocked.Exchange(ref subscribers, Terminated))
                {
                    inner.OnComplete();
                }
            }
        }

        public void OnError(Exception cause)
        {
            if (cause == null)
            {
                throw new ArgumentNullException(nameof(cause));
            }
            if (Interlocked.CompareExchange(ref once, 1, 0) == 0)
            {
                error = cause;
                foreach (var inner in Interlocked.Exchange(ref subscribers, Terminated))
                {
                    inner.OnComplete();
                }
            }
        }

        public void OnNext(T element)
        {
            if (element == null)
            {
                throw new ArgumentNullException(nameof(element));
            }
            foreach (var inner in Volatile.Read(ref subscribers))
            {
                inner.OnNext(element);
            }
        }

        public bool Offer(T element)
        {
            if (element == null)
            {
                throw new ArgumentNullException(nameof(element));
            }
            var a = Volatile.Read(ref subscribers);
            foreach (var inner in a)
            {
                if (inner.IsFull())
                {
                    return false;
                }
            }
            foreach (var inner in a)
            {
                inner.OnNext(element);
            }
            return true;
        }

        public void OnSubscribe(ISubscription subscription)
        {
            if (SubscriptionHelper.SetOnce(ref upstream, subscription, crash: false))
            {
                if (Volatile.Read(ref subscribers) == Terminated)
                {
                    subscription.Cancel();
                }
                else
                {
                    subscription.Request(long.MaxValue);
                }
            }
        }

        public void Subscribe(ISubscriber<T> subscriber)
        {
            if (subscriber == null)
            {
                throw new ArgumentNullException(nameof(subscriber));
            }
            if (subscriber is IFlowableSubscriber<T> s)
            {
                Subscribe(s);
            }
            else
            {
                Subscribe(new StrictSubscriber<T>(subscriber));
            }
        }

        bool Add(ProcessorSubscription inner)
        {
            for (;;)
            {
                var a = Volatile.Read(ref subscribers);
                if (a == Terminated)
                {
                    return false;
                }
                int n = a.Length;
                var b = new ProcessorSubscription[n + 1];
                Array.Copy(a, 0, b, 0, n);
                b[n] = inner;
                if (Interlocked.CompareExchange(ref subscribers, b, a) == a)
                {
                    return true;
                }
            }
        }

        void Remove(ProcessorSubscription inner)
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
                ProcessorSubscription[] b;
                if (n == 1)
                {
                    b = Empty;
                }
                else
                {
                    b = new ProcessorSubscription[n - 1];
                    Array.Copy(a, 0, b, 0, j);
                    Array.Copy(a, j + 1, b, j, n - j - 1);
                }
                if (Interlocked.CompareExchange(ref subscribers, b, a) == a)
                {
                    break;
                }
            }
        }

        public void Subscribe(IFlowableSubscriber<T> subscriber)
        {
            ProcessorSubscription ps = new ProcessorSubscription(subscriber, this);
            subscriber.OnSubscribe(ps);
            if (Add(ps))
            {
                if (ps.IsCancelled())
                {
                    Remove(ps);
                    return;
                }
            }
            else
            {
                Exception ex = error;
                if (ex != null)
                {
                    ps.OnError(ex);
                }
                else
                {
                    ps.OnComplete();
                }
            }
        }

        sealed class ProcessorSubscription : ISubscription
        {
            readonly IFlowableSubscriber<T> actual;

            readonly DirectProcessor<T> parent;

            long requested;

            long emitted;

            internal ProcessorSubscription(IFlowableSubscriber<T> actual, DirectProcessor<T> parent)
            {
                this.actual = actual;
                this.parent = parent;
            }

            internal bool IsFull()
            {
                return Volatile.Read(ref requested) == emitted;
            }

            public void Cancel()
            {
                if (Interlocked.Exchange(ref requested, long.MinValue) != long.MinValue)
                {
                    parent.Remove(this);
                }
            }

            public void Request(long n)
            {
                if (SubscriptionHelper.Validate(n))
                {
                    for (;;)
                    {
                        long r = Volatile.Read(ref requested);
                        if (r == long.MinValue)
                        {
                            break;
                        }
                        long u = r + n;
                        if (u < 0L)
                        {
                            u = long.MaxValue;
                        }
                        if (Interlocked.CompareExchange(ref requested, u, r) == r)
                        {
                            break;
                        }
                    }
                }
            }

            internal void OnNext(T element)
            {
                var r = Volatile.Read(ref requested);
                if (r != long.MinValue)
                {
                    long e = emitted;
                    if (r != e)
                    {
                        actual.OnNext(element);
                        emitted = e + 1;
                    }
                    else
                    {
                        if (Interlocked.Exchange(ref requested, long.MinValue) != long.MinValue)
                        {
                            parent.Remove(this);
                            actual.OnError(new InvalidOperationException("Could not emit value due to lack of requests"));
                        }
                    }
                }
            }

            internal void OnError(Exception cause)
            {
                if (Volatile.Read(ref requested) != long.MinValue)
                {
                    actual.OnError(cause);
                }
            }

            internal void OnComplete()
            {
                if (Volatile.Read(ref requested) != long.MinValue)
                {
                    actual.OnComplete();
                }
            }

            internal bool IsCancelled()
            {
                return Volatile.Read(ref requested) == long.MinValue;
            }
        }
    }
}
