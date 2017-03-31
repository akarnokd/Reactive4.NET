using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Reactive.Streams;
using System.Collections.Concurrent;
using System.Threading;
using Reactive4.NET.utils;

namespace Reactive4.NET.operators
{
    sealed class FlowableGroupBy<T, K, V> : AbstractFlowableOperator<T, IGroupedFlowable<K, V>>
    {
        readonly Func<T, K> keySelector;

        readonly Func<T, V> valueSelector;
        
        public FlowableGroupBy(IFlowable<T> source, Func<T, K> keySelector, Func<T, V> valueSelector) : base(source)
        {
            this.keySelector = keySelector;
            this.valueSelector = valueSelector;
        }

        public override void Subscribe(IFlowableSubscriber<IGroupedFlowable<K, V>> subscriber)
        {
            throw new NotImplementedException();
        }

        sealed class GroupBySubscriber : IFlowableSubscriber<T>, IQueueSubscription<IGroupedFlowable<K, V>>
        {
            readonly ISubscriber<IGroupedFlowable<K, V>> actual;

            readonly Func<T, K> keySelector;

            readonly Func<T, V> valueSelector;

            readonly ISimpleQueue<GroupedFlowable> queue;

            readonly ConcurrentDictionary<K, GroupedFlowable> groups;

            readonly int bufferSize;

            ISubscription upstream;

            int cancelled;
            bool done;
            Exception error;

            int wip;

            bool outputFused;

            long requested;

            int active;

            internal GroupBySubscriber(ISubscriber<IGroupedFlowable<K, V>> actual, Func<T, K> keySelector, Func<T, V> valueSelector, int bufferSize)
            {
                this.actual = actual;
                this.keySelector = keySelector;
                this.valueSelector = valueSelector;
                this.groups = new ConcurrentDictionary<K, GroupedFlowable>();
                this.bufferSize = bufferSize;
                this.active = 1;
                this.queue = new SpscLinkedArrayQueue<GroupedFlowable>(bufferSize);
            }

            public void Cancel()
            {
                if (Interlocked.CompareExchange(ref cancelled, 1, 0) == 0)
                {
                    if (Interlocked.Decrement(ref active) == 0)
                    {
                        upstream.Cancel();
                    }
                }
            }

            public void Clear()
            {
                queue.Clear();
            }

            public bool IsEmpty()
            {
                return queue.IsEmpty();
            }

            public bool Offer(IGroupedFlowable<K, V> item)
            {
                throw new InvalidOperationException("Should not be called!");
            }

            public void OnComplete()
            {
                if (Volatile.Read(ref done))
                {
                    return;
                }
                foreach (var g in groups)
                {
                    g.Value.OnComplete();
                }
                groups.Clear();
                Volatile.Write(ref done, true);
                Drain();
            }

            public void OnError(Exception cause)
            {
                if (Volatile.Read(ref done))
                {
                    return;
                }
                foreach (var g in groups)
                {
                    g.Value.OnError(cause);
                }
                groups.Clear();
                this.error = cause;
                Volatile.Write(ref done, true);
                Drain();
            }

            public void OnNext(T element)
            {
                if (Volatile.Read(ref done))
                {
                    return;
                }

                K key;
                V value;

                try
                {
                    key = keySelector(element);
                    value = valueSelector(element);
                }
                catch (Exception ex)
                {
                    upstream.Cancel();
                    OnError(ex);
                    return;
                }

                if (groups.TryGetValue(key, out GroupedFlowable g))
                {
                    g.OnNext(value);
                }
                else
                {
                    if (Volatile.Read(ref cancelled) == 0)
                    {
                        Interlocked.Increment(ref active);

                        g = new GroupedFlowable(key, bufferSize, this);
                        g.OnNext(value);

                        groups.TryAdd(key, g);

                        queue.Offer(g);
                        Drain();
                    }
                }
            }

            public void OnSubscribe(ISubscription subscription)
            {
                if (SubscriptionHelper.Validate(ref upstream, subscription))
                {
                    actual.OnSubscribe(this);

                    subscription.Request(bufferSize);
                }
            }

            internal void RemoveGroup(K key)
            {
                if (groups.TryRemove(key, out var g))
                {
                    if (Interlocked.Decrement(ref active) == 0)
                    {
                        upstream.Cancel();
                    }
                }
            }

            internal void RequestInner(long n)
            {
                upstream.Request(n);
            }

            public bool Poll(out IGroupedFlowable<K, V> item)
            {
                if (queue.Poll(out GroupedFlowable g))
                {
                    item = g;
                    return true;
                }
                item = default(IGroupedFlowable<K, V>);
                return false;
            }

            public void Request(long n)
            {
                if (SubscriptionHelper.Validate(n))
                {
                    SubscriptionHelper.AddRequest(ref requested, n);
                    Drain();
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

            void Drain()
            {
                if (Interlocked.Increment(ref wip) != 1)
                {
                    return;
                }
                if (outputFused)
                {
                    DrainFused();
                }
                else
                {
                    DrainNormal();
                }
            }

            void DrainFused()
            {

            }

            void DrainNormal()
            {

            }

            sealed class GroupedFlowable : IGroupedFlowable<K, V>, IQueueSubscription<V>
            {
                readonly K key;

                readonly ISimpleQueue<V> queue;

                GroupBySubscriber parent;

                bool done;
                bool cancelled;
                Exception error;

                int wip;

                long requested;

                bool outputFused;

                IFlowableSubscriber<V> actual;

                int once;

                internal GroupedFlowable(K key, int bufferSize, GroupBySubscriber parent)
                {
                    this.key = key;
                    this.parent = parent;
                    this.queue = new SpscArrayQueue<V>(bufferSize);
                }

                public K Key => key;

                public void Cancel()
                {
                    Volatile.Write(ref cancelled, true);
                    Interlocked.Exchange(ref parent, null)?.RemoveGroup(key);
                }

                public void Clear()
                {
                    queue.Clear();
                }

                public bool IsEmpty()
                {
                    return queue.IsEmpty();
                }

                public bool Offer(V item)
                {
                    throw new InvalidOperationException("Should not be called!");
                }

                public void OnComplete()
                {
                    if (Volatile.Read(ref cancelled))
                    {
                        return;
                    }
                    Volatile.Write(ref done, true);
                    Drain();
                }

                public void OnError(Exception cause)
                {
                    if (Volatile.Read(ref cancelled))
                    {
                        return;
                    }
                    error = cause;
                    Volatile.Write(ref done, true);
                    Drain();
                }

                public void OnNext(V element)
                {
                    if (Volatile.Read(ref cancelled))
                    {
                        return;
                    }
                    queue.Offer(element);
                    Drain();
                }

                public bool Poll(out V item)
                {
                    return queue.Poll(out item);
                }

                public void Request(long n)
                {
                    if (SubscriptionHelper.Validate(n))
                    {
                        SubscriptionHelper.AddRequest(ref requested, n);
                        Drain();
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

                void Drain()
                {
                    if (Interlocked.Increment(ref wip) != 1)
                    {
                        return;
                    }
                    if (outputFused)
                    {
                        DrainFused();
                    }
                    else
                    {
                        DrainNormal();
                    }
                }

                void DrainFused()
                {

                }

                void DrainNormal()
                {

                }

                public void Subscribe(IFlowableSubscriber<V> subscriber)
                {
                    if (Interlocked.CompareExchange(ref once, 1, 0) == 0)
                    {
                        subscriber.OnSubscribe(this);
                        Interlocked.Exchange(ref actual, subscriber);
                        if (Volatile.Read(ref cancelled))
                        {
                            Volatile.Write(ref actual, null);
                        }
                    }
                    else
                    {
                        subscriber.OnSubscribe(EmptySubscription<V>.Instance);
                        subscriber.OnError(new InvalidOperationException("This IGroupedFlowable supports at most one ISubscriber only"));
                    }
                }

                public void Subscribe(ISubscriber<V> subscriber)
                {
                    if (subscriber == null)
                    {
                        throw new ArgumentNullException(nameof(subscriber));
                    }
                    if (subscriber is IFlowableSubscriber<V> s)
                    {
                        Subscribe(s);
                    }
                    else
                    {
                        Subscribe(new StrictSubscriber<V>(subscriber));
                    }
                }
            }
        }
    }
}
