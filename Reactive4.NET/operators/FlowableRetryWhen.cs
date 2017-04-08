using Reactive.Streams;
using Reactive4.NET.utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Reactive4.NET.operators
{
    sealed class FlowableRetryWhen<T, U> : AbstractFlowableOperator<T, T>
    {
        readonly Func<IFlowable<Exception>, IPublisher<U>> handler;

        static readonly object Completed = new object();

        public FlowableRetryWhen(IFlowable<T> source, Func<IFlowable<Exception>, IPublisher<U>> handler) : base(source)
        {
            this.handler = handler;
        }

        public override void Subscribe(IFlowableSubscriber<T> subscriber)
        {
            var dp = new DirectProcessor<Exception>();

            IPublisher<U> p;
            try
            {
                p = handler(dp);
            }
            catch (Exception ex)
            {
                subscriber.OnSubscribe(EmptySubscription<T>.Instance);
                subscriber.OnError(ex);
                return;
            }

            var parent = new RetryWhenSubscriber(subscriber, dp, source);
            subscriber.OnSubscribe(parent);
            p.Subscribe(parent.handler);
            parent.Subscribe();
        }

        sealed class RetryWhenSubscriber : SubscriptionArbiter, IFlowableSubscriber<T>, ISubscription
        {
            readonly IFlowableSubscriber<T> actual;

            readonly IFlowableProcessor<Exception> signaller;

            internal readonly HandlerSubscriber handler;

            readonly IFlowable<T> source;

            long produced;

            int wipSubscribe;
            int wip;
            Exception error;

            bool active;

            internal RetryWhenSubscriber(IFlowableSubscriber<T> actual, IFlowableProcessor<Exception> signaller, IFlowable<T> source)
            {
                this.actual = actual;
                this.signaller = signaller;
                this.source = source;
                this.handler = new HandlerSubscriber(this);
            }

            public override void Cancel()
            {
                base.Cancel();
                handler.Cancel();
            }

            public void OnComplete()
            {
                handler.Cancel();
                SerializationHelper.OnComplete(actual, ref wip, ref error);
            }

            public void OnError(Exception cause)
            {
                Volatile.Write(ref active, false);
                handler.Request();
                signaller.OnNext(cause);
            }

            public void OnNext(T element)
            {
                produced++;
                SerializationHelper.OnNext(actual, ref wip, ref error, element);
            }

            public void OnSubscribe(ISubscription subscription)
            {
                ArbiterSet(subscription);
            }

            internal void Subscribe()
            {
                if (Interlocked.Increment(ref wipSubscribe) == 1)
                {
                    do
                    {
                        if (ArbiterIsCancelled())
                        {
                            return;
                        }
                        if (!Volatile.Read(ref active))
                        {
                            long p = produced;
                            if (p != 0L)
                            {
                                produced = 0L;
                                ArbiterProduced(p);
                            }
                            Volatile.Write(ref active, true);
                            source.Subscribe(this);
                        }
                    }
                    while (Interlocked.Decrement(ref wipSubscribe) != 0);
                }
            }

            internal void HandlerComplete()
            {
                base.Cancel();
                SerializationHelper.OnComplete(actual, ref wip, ref error);
            }

            internal void HandlerError(Exception cause)
            {
                base.Cancel();
                SerializationHelper.OnError(actual, ref wip, ref error, cause);
            }

            internal sealed class HandlerSubscriber : IFlowableSubscriber<U>
            {
                readonly RetryWhenSubscriber parent;

                ISubscription upstream;

                long requested;

                internal HandlerSubscriber(RetryWhenSubscriber parent)
                {
                    this.parent = parent;
                }

                public void OnComplete()
                {
                    parent.HandlerComplete();
                }

                public void OnError(Exception cause)
                {
                    parent.HandlerError(cause);
                }

                public void OnNext(U element)
                {
                    parent.Subscribe();
                }

                public void OnSubscribe(ISubscription subscription)
                {
                    SubscriptionHelper.DeferredSetOnce(ref upstream, ref requested, subscription);                }

                internal void Cancel()
                {
                    SubscriptionHelper.Cancel(ref upstream);
                }

                internal void Request()
                {
                    SubscriptionHelper.DeferredRequest(ref upstream, ref requested, 1);
                }
            }
        }
    }
}
