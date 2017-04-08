using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Reactive.Streams;
using System.Threading;
using Reactive4.NET.subscribers;

namespace Reactive4.NET.operators
{
    sealed class FlowableProcessorRefCount<T> : IFlowableProcessor<T>
    {
        readonly IFlowableProcessor<T> source;

        internal FlowableProcessorRefCount(IFlowableProcessor<T> source)
        {
            this.source = source;
        }

        public bool HasComplete => source.HasComplete;

        public bool HasException => source.HasException;

        public Exception Exception => source.Exception;

        public bool HasSubscribers => source.HasSubscribers;

        int count;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref count, int.MinValue) != int.MinValue) { 
                source.Dispose();
            }
        }

        /// <summary>
        /// Returns true if the source IFlowableProcessor has been disposed.
        /// </summary>
        public bool IsDisposed => Volatile.Read(ref count) == int.MinValue;

        public void OnComplete()
        {
            if (Interlocked.Exchange(ref count, int.MinValue) != int.MinValue)
            {
                source.OnComplete();
            }
        }

        public void OnError(Exception cause)
        {
            if (Interlocked.Exchange(ref count, int.MinValue) != int.MinValue)
            {
                source.OnError(cause);
            }
        }

        public void OnNext(T element)
        {
            if (Volatile.Read(ref count) != int.MinValue)
            {
                source.OnNext(element);
            }
        }

        public void OnSubscribe(ISubscription subscription)
        {
            source.OnSubscribe(subscription); // TODO fusion?!
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
            for (;;)
            {
                int c = Volatile.Read(ref count);
                if (c == int.MinValue)
                {
                    source.Subscribe(subscriber);
                    break;
                }
                if (Interlocked.CompareExchange(ref count, c + 1, c) == c)
                {
                    if (subscriber is IConditionalSubscriber<T> s)
                    {
                        source.Subscribe(new RefCountConditionalSubscriber(s, this));
                    }
                    else
                    {
                        source.Subscribe(new RefCountSubscriber(subscriber, this));
                    }
                    break;
                }
            }
        }

        void RemoveOne()
        {
            for (;;)
            {
                int c = Volatile.Read(ref count);
                if (c == int.MinValue)
                {
                    break;
                }
                if (c == 1 && Interlocked.CompareExchange(ref count, int.MinValue, 1) == 1)
                {
                    source.Dispose();
                    break;
                }
                if (Interlocked.CompareExchange(ref count, c - 1, c) == c)
                {
                    break;
                }
            }
        }

        sealed class RefCountSubscriber : AbstractFuseableSubscriber<T, T>
        {
            readonly IFlowableSubscriber<T> actual;

            readonly FlowableProcessorRefCount<T> parent;

            int once;

            internal RefCountSubscriber(IFlowableSubscriber<T> actual, FlowableProcessorRefCount<T> parent)
            {
                this.actual = actual;
                this.parent = parent;
            }

            public override void OnComplete()
            {
                actual.OnComplete();
            }

            public override void OnError(Exception cause)
            {
                actual.OnError(cause);
            }

            public override void OnNext(T element)
            {
                actual.OnNext(element);
            }

            public override bool Poll(out T item)
            {
                return qs.Poll(out item);
            }

            protected override void OnStart(ISubscription subscription)
            {
                actual.OnSubscribe(this);
            }

            public override void Cancel()
            {
                base.Cancel();
                if (Interlocked.CompareExchange(ref once, 1, 0) == 0)
                {
                    parent.RemoveOne();
                }
            }
        }

        sealed class RefCountConditionalSubscriber : AbstractFuseableConditionalSubscriber<T, T>
        {
            readonly IConditionalSubscriber<T> actual;

            readonly FlowableProcessorRefCount<T> parent;

            int once;

            internal RefCountConditionalSubscriber(IConditionalSubscriber<T> actual, FlowableProcessorRefCount<T> parent)
            {
                this.actual = actual;
                this.parent = parent;
            }

            public override void OnComplete()
            {
                actual.OnComplete();
            }

            public override void OnError(Exception cause)
            {
                actual.OnError(cause);
            }

            public override void OnNext(T element)
            {
                actual.OnNext(element);
            }

            public override bool Poll(out T item)
            {
                return qs.Poll(out item);
            }

            protected override void OnStart(ISubscription subscription)
            {
                actual.OnSubscribe(this);
            }

            public override void Cancel()
            {
                base.Cancel();
                if (Interlocked.CompareExchange(ref once, 1, 0) == 0)
                {
                    parent.RemoveOne();
                }
            }

            public override bool TryOnNext(T element)
            {
                return actual.TryOnNext(element);
            }
        }
    }
}
