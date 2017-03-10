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
    sealed class FlowableConcatEnumerable<T> : AbstractFlowableSource<T>
    {
        readonly IEnumerable<IPublisher<T>> sources;

        internal FlowableConcatEnumerable(IEnumerable<IPublisher<T>> sources)
        {
            this.sources = sources;
        }

        public override void Subscribe(IFlowableSubscriber<T> subscriber)
        {
            IEnumerator<IPublisher<T>> enumerator;

            try
            {
                enumerator = sources.GetEnumerator();
            }
            catch (Exception ex)
            {
                subscriber.OnSubscribe(EmptySubscription<T>.Instance);
                subscriber.OnError(ex);
                return;
            }

            if (subscriber is IConditionalSubscriber<T> s)
            {
                var parent = new ConcatEnumerableConditionalSubscriber(s, enumerator);
                subscriber.OnSubscribe(parent);
                parent.OnComplete();
            }
            else
            {
                var parent = new ConcatEnumerableSubscriber(subscriber, enumerator);
                subscriber.OnSubscribe(parent);
                parent.OnComplete();
            }
        }

        sealed class ConcatEnumerableSubscriber : SubscriptionArbiter, IFlowableSubscriber<T>
        {
            readonly IFlowableSubscriber<T> actual;

            IEnumerator<IPublisher<T>> sources;

            int wip;

            long consumed;

            internal ConcatEnumerableSubscriber(IFlowableSubscriber<T> actual, IEnumerator<IPublisher<T>> sources)
            {
                this.actual = actual;
                this.sources = sources;
            }

            public override void Cancel()
            {
                base.Cancel();
                Dispose();
            }

            void Dispose()
            {
                try
                {
                    Interlocked.Exchange(ref sources, null)?.Dispose();
                }
                catch (ObjectDisposedException)
                {

                }
            }

            public void OnComplete()
            {
                if (Interlocked.Increment(ref wip) == 1)
                {
                    var a = sources;
                    var en = Volatile.Read(ref sources);
                    do
                    {
                        if (en == null || ArbiterIsCancelled())
                        {
                            break;
                        }
                        bool b;

                        try
                        {
                            b = en.MoveNext();
                        }
                        catch (Exception ex)
                        {
                            Dispose();
                            actual.OnError(ex);
                            return;
                        }

                        if (!b)
                        {
                            Dispose();
                            actual.OnComplete();
                            return;
                        }

                        var p = en.Current;
                        if (p == null)
                        {
                            Dispose();
                            actual.OnError(new NullReferenceException("One of the IPublishers was null"));
                            return;
                        }
                        long c = consumed;
                        if (c != 0L)
                        {
                            consumed = 0L;
                            ArbiterProduced(c);
                        }
                        p.Subscribe(this);
                    }
                    while (Interlocked.Decrement(ref wip) != 0);
                }
            }

            public void OnError(Exception cause)
            {
                Dispose();
                actual.OnError(cause);
            }

            public void OnNext(T element)
            {
                consumed++;
                actual.OnNext(element);
            }

            public void OnSubscribe(ISubscription subscription)
            {
                ArbiterSet(subscription);
            }
        }

        sealed class ConcatEnumerableConditionalSubscriber : SubscriptionArbiter, IConditionalSubscriber<T>
        {
            readonly IConditionalSubscriber<T> actual;

            IEnumerator<IPublisher<T>> sources;

            int wip;

            long consumed;

            internal ConcatEnumerableConditionalSubscriber(IConditionalSubscriber<T> actual, IEnumerator<IPublisher<T>> sources)
            {
                this.actual = actual;
                this.sources = sources;
            }

            public override void Cancel()
            {
                base.Cancel();
                Dispose();
            }

            void Dispose()
            {
                try
                {
                    Interlocked.Exchange(ref sources, null)?.Dispose();
                }
                catch (ObjectDisposedException)
                {

                }
            }

            public void OnComplete()
            {
                if (Interlocked.Increment(ref wip) == 1)
                {
                    var a = sources;
                    var en = Volatile.Read(ref sources);
                    do
                    {
                        if (en == null || ArbiterIsCancelled())
                        {
                            break;
                        }
                        bool b;

                        try
                        {
                            b = en.MoveNext();
                        }
                        catch (Exception ex)
                        {
                            Dispose();
                            actual.OnError(ex);
                            return;
                        }

                        if (!b)
                        {
                            Dispose();
                            actual.OnComplete();
                            return;
                        }

                        var p = en.Current;
                        if (p == null)
                        {
                            Dispose();
                            actual.OnError(new NullReferenceException("One of the IPublishers was null"));
                            return;
                        }
                        long c = consumed;
                        if (c != 0L)
                        {
                            consumed = 0L;
                            ArbiterProduced(c);
                        }
                        p.Subscribe(this);
                    }
                    while (Interlocked.Decrement(ref wip) != 0);
                }
            }

            public void OnError(Exception cause)
            {
                Dispose();
                actual.OnError(cause);
            }

            public void OnNext(T element)
            {
                consumed++;
                actual.OnNext(element);
            }

            public bool TryOnNext(T element)
            {
                if (actual.TryOnNext(element))
                {
                    consumed++;
                    return true;
                }
                return false;
            }

            public void OnSubscribe(ISubscription subscription)
            {
                ArbiterSet(subscription);
            }
        }
    }
}
