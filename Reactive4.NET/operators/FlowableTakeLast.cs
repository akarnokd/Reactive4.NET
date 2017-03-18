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
    sealed class FlowableTakeLast<T> : AbstractFlowableOperator<T, T>
    {
        readonly int n;

        public FlowableTakeLast(IFlowable<T> source, int n) : base(source)
        {
            this.n = n;
        }

        public override void Subscribe(IFlowableSubscriber<T> subscriber)
        {
            source.Subscribe(new TakeLastSubscriber(subscriber, n));
        }

        sealed class TakeLastSubscriber : IFlowableSubscriber<T>, IQueueSubscription<T>
        {
            readonly IFlowableSubscriber<T> actual;

            readonly int n;

            readonly ArrayQueue<T> queue;

            ISubscription upstream;

            bool done;
            bool cancelled;

            long requested;

            bool outputFused;

            internal TakeLastSubscriber(IFlowableSubscriber<T> actual, int n)
            {
                this.actual = actual;
                this.n = n;
                this.queue = new ArrayQueue<T>();
            }

            public void Cancel()
            {
                Volatile.Write(ref cancelled, true);
                upstream.Cancel();
            }

            public void Clear()
            {
                if (Volatile.Read(ref done))
                {
                    queue.Clear();
                }
            }

            public bool IsEmpty()
            {
                return Volatile.Read(ref done) && queue.IsEmpty();
            }

            public bool Offer(T item)
            {
                throw new InvalidOperationException("Should not be called");
            }

            public void OnComplete()
            {
                Volatile.Write(ref done, true);
                if (outputFused)
                {
                    actual.OnNext(default(T));
                    actual.OnComplete();
                }
                else
                {
                    SubscriptionHelper.PostCompleteMultiResult(actual, ref requested, queue, ref cancelled);
                }
            }

            public void OnError(Exception cause)
            {
                queue.Clear();
                actual.OnError(cause);
            }

            public void OnNext(T element)
            {
                var q = queue;
                if (q.Count == n)
                {
                    q.Poll(out T v);
                }
                q.Offer(element);
            }

            public void OnSubscribe(ISubscription subscription)
            {
                if (SubscriptionHelper.Validate(ref upstream, subscription))
                {
                    actual.OnSubscribe(this);

                    subscription.Request(long.MaxValue);
                }
            }

            public bool Poll(out T item)
            {
                if (Volatile.Read(ref done))
                {
                    if (queue.Poll(out item))
                    {
                        return true;
                    }
                }
                item = default(T);
                return false;
            }

            public void Request(long n)
            {
                if (SubscriptionHelper.Validate(n) && !outputFused)
                {
                    SubscriptionHelper.PostCompleteMultiRequest(actual, ref requested, queue, n, ref cancelled);
                }
            }

            public int RequestFusion(int mode)
            {
                if ((mode & FusionSupport.ASYNC) != 0)
                {
                    outputFused = true;
                    return FusionSupport.ASYNC;
                }
                return FusionSupport.NONE;
            }
        }
    }
}
