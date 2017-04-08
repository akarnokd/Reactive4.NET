using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Reactive.Streams;
using System.Threading;

namespace Reactive4.NET.operators
{
    sealed class ConnectableFlowableMulticast<T> : AbstractFlowableOperator<T, T>, IConnectableFlowable<T>
    {
        readonly Func<IFlowableProcessor<T>> processorSupplier;

        IFlowableProcessor<T> processor;

        IFlowableProcessor<T> connected;

        public ConnectableFlowableMulticast(IFlowable<T> source, Func<IFlowableProcessor<T>> processorSupplier) : base(source)
        {
            this.processorSupplier = processorSupplier;
            this.processor = processorSupplier();
        }

        public ConnectionState ConnectionState
        {
            get
            {
                var p = Volatile.Read(ref connected);
                if (p == null)
                {
                    return ConnectionState.Fresh;
                }
                if (!p.HasComplete && !p.HasException && !p.IsDisposed)
                {
                    return ConnectionState.Connected;
                }
                return ConnectionState.Terminated;
            }
        }

        public IDisposable Connect(Action<IDisposable> onConnect = null)
        {
            for (;;)
            {
                var p = Volatile.Read(ref connected);
                if (p == null || p.HasException || p.HasComplete || p.IsDisposed)
                {
                    var u = Volatile.Read(ref processor);
                    if (u.HasComplete || u.HasException || u.IsDisposed)
                    {
                        var q = processorSupplier();
                        if (Interlocked.CompareExchange(ref processor, q, u) == u)
                        {
                            if (Interlocked.CompareExchange(ref connected, q, p) == p)
                            {
                                onConnect?.Invoke(q);
                                source.Subscribe(q);
                                return q;
                            }
                        }
                    }
                    else
                    {
                        if (Interlocked.CompareExchange(ref connected, u, p) == p)
                        {
                            onConnect?.Invoke(u);
                            source.Subscribe(u);
                            return u;
                        }
                    }
                }
                else
                {
                    onConnect?.Invoke(p);
                    return p;
                }
            }
        }

        public void Reset()
        {
            var p = Volatile.Read(ref processor);
            if (p.HasComplete || p.HasException || p.IsDisposed)
            {
                var q = processorSupplier();
                Interlocked.CompareExchange(ref processor, q, p);
                Interlocked.CompareExchange(ref connected, null, p);
            }
        }

        public override void Subscribe(IFlowableSubscriber<T> subscriber)
        {
            var p = Volatile.Read(ref processor);
            p.Subscribe(subscriber);
        }

    }
}
