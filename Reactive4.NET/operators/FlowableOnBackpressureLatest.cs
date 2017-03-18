using Reactive.Streams;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Reactive4.NET.operators
{
    sealed class FlowableOnBackpressureLatest<T> : AbstractFlowableOperator<T, T>
    {
        readonly Action<T> onDrop;
        
        public FlowableOnBackpressureLatest(IFlowable<T> source, Action<T> onDrop) : base(source)
        {
            this.onDrop = onDrop;
        }

        public override void Subscribe(IFlowableSubscriber<T> subscriber)
        {
            source.Subscribe(new OnBackpressureLatestSubscriber(subscriber, onDrop));
        }

        sealed class OnBackpressureLatestSubscriber : IFlowableSubscriber<T>, ISubscription
        {
            readonly IFlowableSubscriber<T> actual;

            readonly Action<T> onDrop;

            int wip;

            long requested;

            long emitted;

            ISubscription upstream;

            Node latest;

            bool done;
            Exception error;
            bool cancelled;

            internal OnBackpressureLatestSubscriber(IFlowableSubscriber<T> actual, Action<T> onDrop)
            {
                this.actual = actual;
                this.onDrop = onDrop;
            }

            public void Cancel()
            {
                Volatile.Write(ref cancelled, true);
                upstream.Cancel();
                if (Interlocked.Increment(ref wip) == 1)
                {
                    latest = null;
                }
            }

            public void OnComplete()
            {
                if (done)
                {
                    return;
                }
                Volatile.Write(ref done, true);
                Drain();
            }

            public void OnError(Exception cause)
            {
                if (done)
                {
                    return;
                }
                error = cause;
                Volatile.Write(ref done, true);
                Drain();
            }

            public void OnNext(T element)
            {
                if (done)
                {
                    return;
                }
                Node n = new Node(element);

                Node prev = Interlocked.Exchange(ref latest, n);

                if (prev != null)
                {
                    try
                    {
                        onDrop(prev.item);
                    }
                    catch (Exception ex)
                    {
                        upstream.Cancel();
                        OnError(ex);
                        return;
                    }
                }
                Drain();
            }

            public void OnSubscribe(ISubscription subscription)
            {
                if (SubscriptionHelper.Validate(ref upstream, subscription))
                {
                    actual.OnSubscribe(this);

                    subscription.Request(long.MaxValue);
                }
            }

            public void Request(long n)
            {
                if (SubscriptionHelper.Validate(n))
                {
                    SubscriptionHelper.AddRequest(ref requested, n);
                    Drain();
                }
            }

            void Drain()
            {
                if (Interlocked.Increment(ref wip) != 1)
                {
                    return;
                }

                int missed = 1;
                var a = actual;
                var e = emitted;

                for (;;)
                {
                    long r = Volatile.Read(ref requested);

                    while (e != r)
                    {
                        if (Volatile.Read(ref cancelled))
                        {
                            latest = null;
                            return;
                        }

                        bool d = Volatile.Read(ref done);
                        Node n = Interlocked.Exchange(ref latest, null);
                        bool empty = n == null;
                        
                        if (d && empty)
                        {
                            var ex = error;
                            if (ex == null)
                            {
                                a.OnComplete();
                            }
                            else
                            {
                                a.OnError(ex);
                            }
                            return;
                        }

                        if (empty)
                        {
                            break;
                        }

                        a.OnNext(n.item);

                        e++;
                    }

                    if (e == r)
                    {
                        if (Volatile.Read(ref cancelled))
                        {
                            latest = null;
                            return;
                        }

                        bool d = Volatile.Read(ref done);
                        bool empty = Volatile.Read(ref latest) == null;
                        if (d && empty)
                        {
                            var ex = error;
                            if (ex == null)
                            {
                                a.OnComplete();
                            }
                            else
                            {
                                a.OnError(ex);
                            }
                            return;
                        }
                    }

                    int w = Volatile.Read(ref wip);
                    if (w == missed)
                    {
                        emitted = e;
                        missed = Interlocked.Add(ref wip, -missed);
                        if (missed == 0)
                        {
                            break;
                        }
                    }
                    else
                    {
                        missed = w;
                    }
                }
            }

            sealed class Node
            {
                internal readonly T item;
                internal Node(T item)
                {
                    this.item = item;
                }
            }
        }
    }
}
