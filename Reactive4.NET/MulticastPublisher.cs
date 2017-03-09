using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Reactive.Streams;
using Reactive4.NET.operators;

namespace Reactive4.NET
{
    public sealed class MulticastPublisher<T> : IFlowable<T>
    {
        public bool HasComplete => throw new NotImplementedException();

        public bool HasException => throw new NotImplementedException();

        public Exception Exception => throw new NotImplementedException();

        public bool HasSubscribers => throw new NotImplementedException();

        readonly IExecutorService executor;

        readonly int bufferSize;

        public MulticastPublisher() : this(Executors.Task, Flowable.BufferSize())
        {

        }

        public MulticastPublisher(IExecutorService executor) : this(executor, Flowable.BufferSize())
        {

        }

        public MulticastPublisher(IExecutorService executor, int bufferSize)
        {
            this.executor = executor;
            this.bufferSize = bufferSize;
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
