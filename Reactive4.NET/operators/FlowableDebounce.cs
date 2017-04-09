using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Reactive.Streams;
using Reactive4.NET.utils;
using System.Threading;

namespace Reactive4.NET.operators
{
    sealed class FlowableDebounce<T> : AbstractFlowableOperator<T, T>
    {
        readonly TimeSpan delay;

        readonly IExecutorService executor;

        public FlowableDebounce(IFlowable<T> source, TimeSpan delay, IExecutorService executor) : base(source)
        {
            this.delay = delay;
            this.executor = executor;
        }

        public override void Subscribe(IFlowableSubscriber<T> subscriber)
        {
            source.Subscribe(new DebounceSubscriber(subscriber, delay, executor.Worker));
        }

        sealed class DebounceSubscriber : IFlowableSubscriber<T>, ISubscription
        {
            readonly IFlowableSubscriber<T> actual;

            readonly TimeSpan delay;

            readonly IExecutorWorker worker;

            ISubscription upstream;

            IDisposable timer;

            long requested;

            long emitted;

            static readonly Entry Final = new Entry(default(T), long.MaxValue, true);

            static readonly Entry First = new Entry(default(T), 0, true);

            Entry latest;

            internal DebounceSubscriber(IFlowableSubscriber<T> actual, TimeSpan delay, IExecutorWorker worker)
            {
                this.actual = actual;
                this.delay = delay;
                this.worker = worker;
                this.latest = First;
            }

            public void Cancel()
            {
                Interlocked.Exchange(ref latest, Final);
                upstream.Cancel();
                DisposableHelper.Dispose(ref timer);
                worker.Dispose();
            }

            public void OnComplete()
            {
                DisposableHelper.Dispose(ref timer);
                worker.Schedule(() => Timeout(-1L));
            }

            public void OnError(Exception cause)
            {
                Interlocked.Exchange(ref latest, Final);
                DisposableHelper.Dispose(ref timer);
                worker.Dispose();
            }

            public void OnNext(T element)
            {
                var curr = Volatile.Read(ref latest);
                long idx = curr.index;
                if (idx != long.MaxValue)
                {
                    Entry e = new Entry(element, idx + 1, false);
                    if (Interlocked.CompareExchange(ref latest, e, curr) == curr)
                    {
                        DisposableHelper.Set(ref timer, worker.Schedule(() => Timeout(idx + 1), delay));
                    }
                }
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
                }
            }

            void Timeout(long index)
            {
                var curr = Volatile.Read(ref latest);
                if (index == -1L)
                {
                    Interlocked.Exchange(ref latest, Final);
                    if (!curr.empty && Emit(curr)) {
                        actual.OnComplete();
                        worker.Dispose();
                    }
                }
                else
                if (curr.index == index)
                {
                    Entry entry = new Entry(default(T), index, true);
                    if (Interlocked.CompareExchange(ref latest, entry, curr) == curr)
                    {
                        Emit(curr);
                    }
                }
            }

            bool Emit(Entry curr)
            {
                long e = emitted;
                if (Volatile.Read(ref requested) != e)
                {
                    emitted = e + 1;
                    actual.OnNext(curr.item);
                    return true;
                }
                Interlocked.Exchange(ref latest, Final);
                actual.OnError(new InvalidOperationException("Could not emit value due to lack of requests"));
                DisposableHelper.Dispose(ref timer);
                worker.Dispose();
                return false;
            }

            internal class Entry
            {
                internal readonly T item;
                internal readonly long index;
                internal readonly bool empty;

                internal Entry(T item, long index, bool empty)
                {
                    this.item = item;
                    this.index = index;
                    this.empty = empty;
                }
            }
        }
    }
}
