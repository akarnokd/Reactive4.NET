using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Reactive4.NET.operators
{
    sealed class FlowableAutoConnect<T> : AbstractFlowableSource<T>
    {
        readonly IConnectableFlowable<T> source;

        readonly Action<IDisposable> onConnect;

        int count;

        internal FlowableAutoConnect(IConnectableFlowable<T> source, int count, Action<IDisposable> onConnect)
        {
            this.source = source;
            this.count = count;
            this.onConnect = onConnect;
        }

        public override void Subscribe(IFlowableSubscriber<T> subscriber)
        {
            source.Subscribe(subscriber);
            if (Volatile.Read(ref count) > 0 && Interlocked.Decrement(ref count) == 0)
            {
                source.Connect(onConnect);
            }
        }
    }
}
