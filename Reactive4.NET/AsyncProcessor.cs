using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Reactive.Streams;
using Reactive4.NET.operators;
using System.Threading;
using Reactive4.NET.utils;

namespace Reactive4.NET
{
    public sealed class AsyncProcessor<T> : IFlowableProcessor<T>, IDisposable
    {
        public bool HasComplete
        {
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

        ISubscription upstream;

        int once;
        Exception error;
        T value;
        bool hasValue;

        public void Dispose()
        {
            SubscriptionHelper.Cancel(ref upstream);
        }

        public void OnComplete()
        {
            if (Interlocked.CompareExchange(ref once, 1, 0) == 0)
            {
                if (hasValue)
                {
                    T v = value;
                    foreach (var inner in Interlocked.Exchange(ref subscribers, Terminated))
                    {
                        inner.Complete(v);
                    }
                }
                else
                {
                    foreach (var inner in Interlocked.Exchange(ref subscribers, Terminated))
                    {
                        inner.Complete();
                    }
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
                    inner.Error(cause);
                }
            }
        }

        public void OnNext(T element)
        {
            if (element == null)
            {
                throw new ArgumentNullException(nameof(element));
            }
            if (!hasValue)
            {
                hasValue = true;
            }
            value = element;
        }

        public void OnSubscribe(ISubscription subscription)
        {
            if (SubscriptionHelper.SetOnce(ref upstream, subscription, false))
            {
                if (Volatile.Read(ref subscribers) != Terminated)
                {
                    subscription.Request(long.MaxValue);
                }
                else
                {
                    subscription.Cancel();
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

        public void Subscribe(IFlowableSubscriber<T> subscriber)
        {
            var ps = new ProcessorSubscription(subscriber, this);
            subscriber.OnSubscribe(ps);
            if (Add(ps))
            {
                if (ps.IsCancelled())
                {
                    Remove(ps);
                }
            }
            else
            {
                Exception ex = error;
                if (ex != null)
                {
                    ps.Error(ex);
                } else
                if (hasValue)
                {
                    ps.Complete(value);
                } else
                {
                    ps.Complete();
                }
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

        sealed class ProcessorSubscription : AbstractDeferredScalarSubscription<T>
        {
            readonly AsyncProcessor<T> parent;

            public ProcessorSubscription(IFlowableSubscriber<T> actual, AsyncProcessor<T> parent) : base(actual)
            {
                this.parent = parent;
            }

            public override void Cancel()
            {
                if (!IsCancelled())
                {
                    base.Cancel();
                    parent.Remove(this);
                }
            }
        }
    }
}
