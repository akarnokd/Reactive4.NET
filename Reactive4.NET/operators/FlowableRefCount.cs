using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Reactive.Streams;
using System.Threading;
using Reactive4.NET.utils;

namespace Reactive4.NET.operators
{
    sealed class FlowableRefCount<T> : AbstractFlowableSource<T>
    {
        readonly IConnectableFlowable<T> source;

        readonly int count;

        int state;
        SequentialDisposable connection;
        bool connected;


        internal FlowableRefCount(IConnectableFlowable<T> source, int count)
        {
            this.source = source;
            this.count = count;
            this.connection = new SequentialDisposable();
        }

        public override void Subscribe(IFlowableSubscriber<T> subscriber)
        {
            bool connect = false;
            SequentialDisposable sd;
            lock (this)
            {
                sd = connection;
                subscriber = new RefCountSubscriber(subscriber, this, sd);
                int c = ++state;
                if (!connected && c == count)
                {
                    connected = true;
                    connect = true;
                }
            }
            source.Subscribe(subscriber);
            if (connect)
            {
                source.Connect(d => sd.Replace(d));
            }
        }

        void Remove(IDisposable connection)
        {
            lock (this)
            {
                if (this.connection == connection)
                {
                    if (--state == 0)
                    {
                        this.connection = new SequentialDisposable();
                        this.connected = false;
                        connection.Dispose();
                        source.Reset();
                    }
                    else
                    {
                        return;
                    }
                }
                else
                {
                    return;
                }
            }
        }

        void Terminate(IDisposable connection)
        {
            lock (this)
            {
                if (this.connection == connection)
                {
                    this.connection = new SequentialDisposable();
                    state = 0;
                    this.connected = false;
                    source.Reset();
                }
            }
        }

        sealed class RefCountSubscriber : IFlowableSubscriber<T>, ISubscription
        {
            readonly IFlowableSubscriber<T> actual;

            readonly FlowableRefCount<T> parent;

            readonly IDisposable connection;

            int once;

            ISubscription upstream;

            internal RefCountSubscriber(IFlowableSubscriber<T> actual, FlowableRefCount<T> parent, IDisposable connection)
            {
                this.actual = actual;
                this.parent = parent;
                this.connection = connection;
            }

            public void Cancel()
            {
                if (Interlocked.CompareExchange(ref once, 1, 0) == 0)
                {
                    parent.Remove(connection);
                    upstream.Cancel();
                }
            }

            public void OnComplete()
            {
                if (Interlocked.CompareExchange(ref once, 1, 0) == 0)
                {
                    parent.Terminate(connection);
                }
                actual.OnComplete();
            }

            public void OnError(Exception cause)
            {
                if (Interlocked.CompareExchange(ref once, 1, 0) == 0)
                {
                    parent.Terminate(connection);
                }
                actual.OnError(cause);
            }

            public void OnNext(T element)
            {
                actual.OnNext(element);
            }

            public void OnSubscribe(ISubscription subscription)
            {
                if (SubscriptionHelper.Validate(ref upstream, subscription))
                {
                    actual.OnSubscribe(this);
                }
            }

            public void Request(long n)
            {
                upstream.Request(n);
            }
        }
    }
}
