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
    public sealed class UnicastProcessor<T> : IFlowableProcessor<T>
    {
        public bool HasComplete => throw new NotImplementedException();

        public bool HasException => throw new NotImplementedException();

        public Exception Exception => throw new NotImplementedException();

        public bool HasSubscribers => throw new NotImplementedException();

        readonly int bufferSize;

        Action onTerminate;

        public UnicastProcessor() : this(Flowable.BufferSize(), () => { })
        {

        }

        public UnicastProcessor(int bufferSize) : this(bufferSize, () => { })
        {

        }

        public UnicastProcessor(int bufferSize, Action onTerminate)
        {
            this.bufferSize = bufferSize;
            Volatile.Write(ref this.onTerminate, onTerminate);
        }

        void Terminate()
        {
            Action a = Interlocked.Exchange(ref onTerminate, null);
            a?.Invoke();
        }

        public void OnComplete()
        {
            throw new NotImplementedException();
        }

        public void OnError(Exception cause)
        {
            throw new NotImplementedException();
        }

        public void OnNext(T element)
        {
            throw new NotImplementedException();
        }

        public void OnSubscribe(ISubscription subscription)
        {
            throw new NotImplementedException();
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
            throw new NotImplementedException();
        }
    }
}
