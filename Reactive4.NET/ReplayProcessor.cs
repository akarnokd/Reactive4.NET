using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Reactive.Streams;
using Reactive4.NET.operators;
using System.Threading;

namespace Reactive4.NET
{
    public sealed class ReplayProcessor<T> : IFlowableProcessor<T>
    {
        public bool HasComplete => manager.HasTerminated && manager.Error == null;

        public bool HasException => manager.HasTerminated && manager.Error != null;

        public Exception Exception => manager.HasTerminated ? manager.Error : null;

        public bool HasSubscribers => manager.HasSubscribers;

        public static ReplayProcessor<T> CreateUnbounded(int capacityHint)
        {
            if (capacityHint <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capacityHint), capacityHint, "Positive value expected");
            }
            return new ReplayProcessor<T>(new UnboundedBufferManager(capacityHint));
        }

        readonly IBufferManager manager;

        ISubscription upstream;

        public ReplayProcessor()
        {
            manager = new UnboundedBufferManager(10);
        }

        public ReplayProcessor(int maxSize)
        {
            if (maxSize <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxSize), maxSize, "Positive value expected");
            }
            manager = new SizeBoundBufferManager(maxSize);
        }

        public ReplayProcessor(TimeSpan maxAge) : this(int.MaxValue, maxAge, Executors.Computation) { }

        public ReplayProcessor(TimeSpan maxAge, IExecutorService executor) : this(int.MaxValue, maxAge, executor) { }

        public ReplayProcessor(int maxSize, TimeSpan maxAge) : this(maxSize, maxAge, Executors.Computation) { }

        public ReplayProcessor(int maxSize, TimeSpan maxAge, IExecutorService executor)
        {
            this.manager = new TimeBoundBufferManager(maxSize, maxAge, executor);
        }

        internal ReplayProcessor(IBufferManager bufferManager)
        {
            this.manager = bufferManager;
        }

        public void OnComplete()
        {
            manager.OnComplete();
        }

        public void OnError(Exception cause)
        {
            if (cause == null)
            {
                throw new ArgumentNullException(nameof(cause));
            }
            manager.OnError(cause);
        }

        public void OnNext(T element)
        {
            if (element == null)
            {
                throw new ArgumentNullException(nameof(element));
            }
            manager.OnNext(element);
        }

        public void OnSubscribe(ISubscription subscription)
        {
            if (SubscriptionHelper.SetOnce(ref upstream, subscription))
            {
                if (manager.HasTerminated)
                {
                    subscription.Cancel();
                }
                else
                {
                    subscription.Request(long.MaxValue);
                }
            }
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
            manager.Subscribe(subscriber);
        }

        // ------------------------------------------------------------------
        // Buffer API & Implementations
        // ------------------------------------------------------------------

        internal interface IBufferManager
        {
            void OnNext(T element);

            void OnError(Exception cause);

            void OnComplete();

            bool HasTerminated { get; }

            Exception Error { get; }

            bool HasSubscribers { get; }

            void Subscribe(IFlowableSubscriber<T> subscriber);

            void Remove(ProcessorSubscription ps);

            void Replay(ProcessorSubscription ps);

            object DeadNode { get; }

            bool Poll(ProcessorSubscription ps, out T item);

            bool IsEmpty(ProcessorSubscription ps);

            void Clear(ProcessorSubscription ps);
        }

        internal sealed class ProcessorSubscription : IQueueSubscription<T>
        {
            readonly IFlowableSubscriber<T> actual;

            readonly IBufferManager parent;

            internal bool IsCancelled => Requested == long.MinValue;

            internal long Requested
            {
                get
                {
                    return Volatile.Read(ref requested);
                }
            }

            internal long Emitted { get; set; }

            internal object Node { get; set; }

            internal int Offset { get; set; }

            long requested;

            internal int wip;

            bool outputFused;

            internal ProcessorSubscription(IFlowableSubscriber<T> actual, IBufferManager parent)
            {
                this.actual = actual;
                this.parent = parent;
            }

            public void Cancel()
            {
                if (Interlocked.Exchange(ref requested, long.MinValue) != long.MinValue)
                {
                    Node = parent.DeadNode;
                    parent.Remove(this);
                }
            }

            public void Clear()
            {
                throw new NotImplementedException();
            }

            public bool IsEmpty()
            {
                throw new NotImplementedException();
            }

            public bool Offer(T item)
            {
                throw new InvalidOperationException("Should not be called!");
            }

            public bool Poll(out T item)
            {
                throw new NotImplementedException();
            }

            public void Request(long n)
            {
                if (n <= 0L)
                {
                    throw new ArgumentOutOfRangeException(nameof(n), "Positive request amount expected");
                }
                for (;;)
                {
                    long r = Volatile.Read(ref requested);
                    if (r == long.MinValue)
                    {
                        break;
                    }
                    long u = r + n;
                    if (u < 0L)
                    {
                        u = long.MaxValue;
                    }
                    if (Interlocked.CompareExchange(ref requested, u, r) == r)
                    {
                        parent.Replay(this);
                        break;
                    }
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

            internal void OnNext(T element)
            {
                if (Volatile.Read(ref requested) != long.MinValue)
                {
                    actual.OnNext(element);
                }
            }

            internal void OnError(Exception cause)
            {
                if (Volatile.Read(ref requested) != long.MinValue)
                {
                    Node = parent.DeadNode;
                    actual.OnError(cause);
                }
            }

            internal void OnComplete()
            {
                if (Volatile.Read(ref requested) != long.MinValue)
                {
                    Node = parent.DeadNode;
                    actual.OnComplete();
                }
            }
        }

        internal abstract class AbstractBufferManager : IBufferManager
        {
            internal ProcessorSubscription[] subscribers = Empty;

            int once;
            internal bool done;
            internal Exception error;

            internal static readonly ProcessorSubscription[] Empty = new ProcessorSubscription[0];
            internal static readonly ProcessorSubscription[] Terminated = new ProcessorSubscription[0];

            public bool HasTerminated => Volatile.Read(ref subscribers) == Terminated;

            public Exception Error => error;

            public bool HasSubscribers => Volatile.Read(ref subscribers).Length != 0;

            public abstract object DeadNode { get; }

            internal bool Add(ProcessorSubscription inner)
            {
                for (;;)
                {
                    var a = Volatile.Read(ref subscribers);
                    if (a == Terminated)
                    {
                        return false;
                    }
                    int n = a.Length;
                    var b = new ProcessorSubscription[n + 1];
                    Array.Copy(a, 0, b, 0, n);
                    b[n] = inner;
                    if (Interlocked.CompareExchange(ref subscribers, b, a) == a)
                    {
                        return true;
                    }
                }
            }

            public void Subscribe(IFlowableSubscriber<T> subscriber)
            {
                ProcessorSubscription ps = new ProcessorSubscription(subscriber, this);
                subscriber.OnSubscribe(ps);

                if (Add(ps))
                {
                    if (ps.IsCancelled)
                    {
                        Remove(ps);
                        return;
                    }
                }
                Replay(ps);
            }


            public void Remove(ProcessorSubscription inner)
            {
                for (;;)
                {
                    var a = Volatile.Read(ref subscribers);
                    int n = a.Length;
                    if (n == 0)
                    {
                        break;
                    }
                    int j = -1;
                    for (int i = 0; i < n; i++)
                    {
                        if (a[i] == inner)
                        {
                            j = i;
                            break;
                        }
                    }
                    if (j < 0)
                    {
                        break;
                    }
                    ProcessorSubscription[] b;
                    if (n == 1)
                    {
                        b = Empty;
                    }
                    else
                    {
                        b = new ProcessorSubscription[n - 1];
                        Array.Copy(a, 0, b, 0, j);
                        Array.Copy(a, j + 1, b, j, n - j - 1);
                    }
                    if (Interlocked.CompareExchange(ref subscribers, b, a) == a)
                    {
                        break;
                    }
                }
            }

            public void OnComplete()
            {
                if (Interlocked.CompareExchange(ref once, 1, 0) == 0)
                {
                    Volatile.Write(ref done, true);
                    foreach (var ps in Interlocked.Exchange(ref subscribers, Terminated))
                    {
                        Replay(ps);
                    }
                }
            }

            public void OnError(Exception cause)
            {
                if (Interlocked.CompareExchange(ref once, 1, 0) == 0)
                {
                    error = cause;
                    Volatile.Write(ref done, true);
                    foreach (var ps in Interlocked.Exchange(ref subscribers, Terminated))
                    {
                        Replay(ps);
                    }
                }
            }

            public void OnNext(T element)
            {
                AddElement(element);
                foreach (var ps in Volatile.Read(ref subscribers))
                {
                    Replay(ps);
                }
            }

            public abstract void Replay(ProcessorSubscription ps);

            public abstract void AddElement(T element);

            public abstract void Finish();

            public abstract bool Poll(ProcessorSubscription ps, out T item);

            public abstract bool IsEmpty(ProcessorSubscription ps);

            public abstract void Clear(ProcessorSubscription ps);
        }

        sealed class UnboundedBufferManager : AbstractBufferManager
        {
            readonly ArrayNode head;

            ArrayNode tail;

            static readonly ArrayNode Dead = new ArrayNode(0);

            int tailOffset;

            int size;

            internal UnboundedBufferManager(int capacityHint)
            {
                var n = new ArrayNode(capacityHint);
                this.tail = n;
                this.head = n;
            }

            public override object DeadNode => Dead;

            public override void AddElement(T element)
            {
                var t = tail;
                var a = t.array;
                var n = a.Length;
                var to = tailOffset;
                if (to == n)
                {
                    var q = new ArrayNode(n);
                    a = q.array;
                    a[0] = element;
                    tailOffset = 1;
                    t.next = q;
                    tail = q;
                }
                else
                {
                    a[to] = element;
                    tailOffset = to + 1;
                }

                Interlocked.Exchange(ref size, size + 1);
            }

            public override void Clear(ProcessorSubscription ps)
            {
                throw new NotImplementedException();
            }

            public override void Finish()
            {
                // no cleanup required
            }

            public override bool IsEmpty(ProcessorSubscription ps)
            {
                throw new NotImplementedException();
            }

            public override bool Poll(ProcessorSubscription ps, out T item)
            {
                throw new NotImplementedException();
            }

            public override void Replay(ProcessorSubscription ps)
            {
                throw new NotImplementedException();
            }
        }

        internal sealed class ArrayNode
        {
            internal readonly T[] array;

            internal ArrayNode next;

            internal ArrayNode(int capacity)
            {
                this.array = new T[capacity];
            }
        }


        sealed class SizeBoundBufferManager : AbstractBufferManager
        {
            readonly int maxSize;

            internal SizeBoundBufferManager(int maxSize)
            {
                this.maxSize = maxSize;
            }

            public override object DeadNode => throw new NotImplementedException();

            public override void AddElement(T element)
            {
                throw new NotImplementedException();
            }

            public override void Clear(ProcessorSubscription ps)
            {
                throw new NotImplementedException();
            }

            public override void Finish()
            {
                throw new NotImplementedException();
            }

            public override bool IsEmpty(ProcessorSubscription ps)
            {
                throw new NotImplementedException();
            }

            public override bool Poll(ProcessorSubscription ps, out T item)
            {
                throw new NotImplementedException();
            }

            public override void Replay(ProcessorSubscription ps)
            {
                throw new NotImplementedException();
            }
        }

        internal class Node
        {
            internal readonly T item;
            internal Node next;

            internal Node(T item)
            {
                this.item = item;
            }
        }

        sealed class TimeBoundBufferManager : AbstractBufferManager
        {
            readonly int maxSize;

            readonly long maxAgeMillis;

            readonly IExecutorService executor;

            internal TimeBoundBufferManager(int maxSize, TimeSpan maxAge, IExecutorService executor)
            {
                this.maxSize = maxSize;
                this.maxAgeMillis = (long)maxAge.TotalMilliseconds;
                this.executor = executor;
            }

            public override object DeadNode => throw new NotImplementedException();

            public override void AddElement(T element)
            {
                throw new NotImplementedException();
            }

            public override void Clear(ProcessorSubscription ps)
            {
                throw new NotImplementedException();
            }

            public override void Finish()
            {
                throw new NotImplementedException();
            }

            public override bool IsEmpty(ProcessorSubscription ps)
            {
                throw new NotImplementedException();
            }

            public override bool Poll(ProcessorSubscription ps, out T item)
            {
                throw new NotImplementedException();
            }

            public override void Replay(ProcessorSubscription ps)
            {
                throw new NotImplementedException();
            }
        }

        internal class TimedNode {
            internal readonly T item;
            internal readonly long timestamp;
            internal TimedNode next;

            internal TimedNode(T item, long timestamp)
            {
                this.item = item;
                this.timestamp = timestamp;
            }
        }
    }
}
