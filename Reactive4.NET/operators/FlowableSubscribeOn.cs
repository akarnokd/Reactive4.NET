using Reactive.Streams;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Reactive4.NET.operators
{
    sealed class FlowableSubscribeOn<T> : AbstractFlowableOperator<T, T>
    {
        readonly IExecutorService executor;

        readonly bool requestOn;

        public FlowableSubscribeOn(IFlowable<T> source, IExecutorService executor, bool requestOn) : base(source)
        {
            this.executor = executor;
            this.requestOn = requestOn;
        }

        public override void Subscribe(IFlowableSubscriber<T> subscriber)
        {
            IExecutorWorker worker = executor.Worker;
            var s = new SubscribeOnSubscriber(subscriber, worker, requestOn, source);
            subscriber.OnSubscribe(s);
            worker.Schedule(s.Run);
        }

        sealed class SubscribeOnSubscriber : IFlowableSubscriber<T>, ISubscription
        {
            readonly IFlowableSubscriber<T> actual;

            readonly IExecutorWorker worker;

            readonly bool requestOn;

            IFlowable<T> source;

            ISubscription upstream;

            long requested;

            internal SubscribeOnSubscriber(IFlowableSubscriber<T> actual, IExecutorWorker worker, bool requestOn, IFlowable<T> source)
            {
                this.actual = actual;
                this.worker = worker;
                this.source = source;
                this.requestOn = requestOn;
            }

            public void Cancel()
            {
                SubscriptionHelper.Cancel(ref upstream);
                worker.Dispose();
            }

            public void OnComplete()
            {
                actual.OnComplete();
                worker.Dispose();
            }

            public void OnError(Exception cause)
            {
                actual.OnError(cause);
                worker.Dispose();
            }

            public void OnNext(T element)
            {
                actual.OnNext(element);
            }

            public void OnSubscribe(ISubscription subscription)
            {
                if (requestOn)
                {
                    if (SubscriptionHelper.SetOnce(ref upstream, subscription))
                    {
                        long r = Interlocked.Exchange(ref requested, 0L);
                        if (r != 0L)
                        {
                            worker.Schedule(() => upstream.Request(r));
                        }
                    }
                }
                else
                {
                    SubscriptionHelper.DeferredSetOnce(ref upstream, ref requested, subscription);
                }
            }

            public void Request(long n)
            {
                if (n <= 0L)
                {
                    throw new ArgumentOutOfRangeException(nameof(n));
                }
                if (requestOn)
                {
                    var s = Volatile.Read(ref upstream);
                    if (s != null)
                    {
                        worker.Schedule(() => upstream.Request(n));
                    }
                    else
                    {
                        SubscriptionHelper.AddRequest(ref requested, n);
                        s = Volatile.Read(ref upstream);
                        if (s != null)
                        {
                            long r = Interlocked.Exchange(ref requested, 0L);
                            if (r != 0L)
                            {
                                worker.Schedule(() => upstream.Request(r));
                            }
                        }
                    }
                }
                else
                {
                    SubscriptionHelper.DeferredRequest(ref upstream, ref requested, n);
                }
            }

            internal void Run()
            {
                var f = source;
                source = null;
                f.Subscribe(this);
            }
        }
    }
}
