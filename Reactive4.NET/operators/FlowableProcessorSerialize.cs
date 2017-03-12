using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Reactive.Streams;
using System.Threading;

namespace Reactive4.NET.operators
{
    sealed class FlowableProcessorSerialize<T> : IFlowableProcessor<T>, ISubscription
    {
        readonly IFlowableProcessor<T> actual;

        internal FlowableProcessorSerialize(IFlowableProcessor<T> actual)
        {
            this.actual = actual;
        }

        public bool HasComplete => actual.HasComplete;

        public bool HasException => actual.HasException;

        public Exception Exception => actual.Exception;

        public bool HasSubscribers => actual.HasSubscribers;

        ISubscription upstream;

        List<T> list;

        bool cancelled;
        bool emitting;
        bool done;
        Exception error;

        public void OnComplete()
        {
            lock (this)
            {
                if (Volatile.Read(ref cancelled))
                {
                    return;
                }
                if (done)
                {
                    return;
                }
                done = true;
                if (emitting)
                {
                    return;
                }
            }
            actual.OnComplete();
        }

        public void OnError(Exception cause)
        {
            if (cause == null)
            {
                throw new ArgumentNullException(nameof(cause));
            }
            lock (this)
            {
                if (Volatile.Read(ref cancelled))
                {
                    return;
                }
                if (done)
                {
                    return;
                }
                done = true;
                if (emitting)
                {
                    error = cause;
                    return;
                }
            }
            actual.OnError(cause);
        }

        public void OnNext(T element)
        {
            if (element == null)
            {
                throw new ArgumentNullException(nameof(element));
            }
            lock (this)
            {
                if (Volatile.Read(ref cancelled))
                {
                    return;
                }
                if (done)
                {
                    return;
                }
                if (emitting)
                {
                    var lst = list;
                    if (lst == null)
                    {
                        lst = new List<T>();
                        list = lst;
                    }
                    lst.Add(element);
                    return;
                }
                emitting = true;
            }

            var a = actual;

            a.OnNext(element);
            for (;;)
            {
                List<T> q;
                Exception ex;
                bool d;
                lock (this)
                {
                    d = done;
                    ex = error;
                    q = list;
                    if (!d && a == null)
                    {
                        break;
                    }
                    list = null;
                }

                foreach (T t in q)
                {
                    if (Volatile.Read(ref cancelled))
                    {
                        return;
                    }

                    a.OnNext(t);
                }

                if (Volatile.Read(ref cancelled))
                {
                    return;
                }

                if (d)
                {
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
        }

        public void OnSubscribe(ISubscription subscription)
        {
            if (SubscriptionHelper.SetOnce(ref upstream, subscription))
            {
                actual.OnSubscribe(this);
            }
        }

        public void Subscribe(IFlowableSubscriber<T> subscriber)
        {
            actual.Subscribe(subscriber);
        }

        public void Subscribe(ISubscriber<T> subscriber)
        {
            actual.Subscribe(subscriber);
        }

        public void Request(long n)
        {
            upstream.Request(n);
        }

        public void Cancel()
        {
            Volatile.Write(ref cancelled, true);
            upstream.Cancel();
        }

        public void Dispose()
        {
            Cancel();
        }
    }
}
