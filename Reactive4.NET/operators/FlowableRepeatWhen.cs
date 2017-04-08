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
    sealed class FlowableRepeatWhen<T, U> : AbstractFlowableOperator<T, T>
    {
        readonly Func<IFlowable<object>, IPublisher<U>> handler;

        static readonly object Completed = new object();

        public FlowableRepeatWhen(IFlowable<T> source, Func<IFlowable<object>, IPublisher<U>> handler) : base(source)
        {
            this.handler = handler;
        }

        public override void Subscribe(IFlowableSubscriber<T> subscriber)
        {
            var dp = new DirectProcessor<object>();

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

            var parent = new RepeatWhenSubscriber(subscriber, dp, source);
            subscriber.OnSubscribe(parent);
            p.Subscribe(parent.handler);
            parent.Subscribe();
        }

        sealed class RepeatWhenSubscriber : SubscriptionArbiter, IFlowableSubscriber<T>, ISubscription
        {
            readonly IFlowableSubscriber<T> actual;

            readonly IFlowableProcessor<object> signaller;

            internal readonly HandlerSubscriber handler;

            readonly IFlowable<T> source;

            long produced;

            int wipSubscribe;
            int wip;
            Exception error;

            bool active;

            internal RepeatWhenSubscriber(IFlowableSubscriber<T> actual, IFlowableProcessor<object> signaller, IFlowable<T> source)
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
                Volatile.Write(ref active, false);
                handler.Request();
                signaller.OnNext(Completed);
            }

            public void OnError(Exception cause)
            {
                handler.Cancel();
                SerializationHelper.OnError(actual, ref wip, ref error, cause);
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
                readonly RepeatWhenSubscriber parent;

                ISubscription upstream;

                long requested;

                internal HandlerSubscriber(RepeatWhenSubscriber parent)
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
