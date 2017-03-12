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
    public sealed class ReplayProcessor<T> : IFlowableProcessor<T>, IDisposable
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

        public void Dispose()
        {
            SubscriptionHelper.Cancel(ref upstream);
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
            if (SubscriptionHelper.SetOnce(ref upstream, subscription, crash: false))
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
        }

        internal sealed class ProcessorSubscription : IQueueSubscription<T>
        {
            readonly IFlowableSubscriber<T> actual;

            readonly IBufferManager parent;

            internal bool IsCancelled => Volatile.Read(ref requested) == long.MinValue;

            internal long requested;

            internal long emitted;

            internal object node;

            internal int offset;

            internal int wip;

            internal bool outputFused;

            internal ProcessorSubscription(IFlowableSubscriber<T> actual, IBufferManager parent)
            {
                this.actual = actual;
                this.parent = parent;
            }

            public void Cancel()
            {
                if (Interlocked.Exchange(ref requested, long.MinValue) != long.MinValue)
                {
                    node = parent.DeadNode;
                    parent.Remove(this);
                }
            }

            public void Clear()
            {
                node = parent.DeadNode;
            }

            public bool IsEmpty()
            {
                return parent.IsEmpty(this);
            }

            public bool Offer(T item)
            {
                throw new InvalidOperationException("Should not be called!");
            }

            public bool Poll(out T item)
            {
                return parent.Poll(this, out item);
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
                    node = parent.DeadNode;
                    actual.OnError(cause);
                }
            }

            internal void OnComplete()
            {
                if (Volatile.Read(ref requested) != long.MinValue)
                {
                    node = parent.DeadNode;
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

            internal int size;

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

            public void Replay(ProcessorSubscription ps)
            {
                if (Interlocked.Increment(ref ps.wip) != 1)
                {
                    return;
                }
                if (ps.outputFused)
                {
                    ReplayFused(ps);
                }
                else
                {
                    ReplayAsync(ps);
                }
            }

            public abstract void AddElement(T element);

            public abstract void Finish();

            public abstract bool Poll(ProcessorSubscription ps, out T item);

            public abstract bool IsEmpty(ProcessorSubscription ps);

            public abstract void ReplayFused(ProcessorSubscription ps);

            public abstract void ReplayAsync(ProcessorSubscription ps);
        }

        sealed class UnboundedBufferManager : AbstractBufferManager
        {
            readonly ArrayNode head;

            ArrayNode tail;

            static readonly ArrayNode Dead = new ArrayNode(0);

            int tailOffset;

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

            public override void Finish()
            {
                // no cleanup required
            }

            public override bool IsEmpty(ProcessorSubscription ps)
            {
                return Volatile.Read(ref size) == ps.emitted;
            }

            public override bool Poll(ProcessorSubscription ps, out T item)
            {
                long e = ps.emitted;
                if (Volatile.Read(ref size) == e)
                {
                    item = default(T);
                    return false;
                }
                int offset = ps.offset;
                var n = ps.node as ArrayNode;
                var a = n.array;
                int m = a.Length;
                if (offset == m)
                {
                    n = n.next;
                    ps.node = n;
                    a = n.array;
                    offset = 0;
                }

                item = a[offset];
                ps.offset = offset + 1;
                ps.emitted = e + 1;
                return true;
            }

            public override void ReplayAsync(ProcessorSubscription ps)
            {
                int missed = 1;
                long e = ps.emitted;
                ArrayNode n = ps.node as ArrayNode;
                int offset = ps.offset;

                if (n == null)
                {
                    n = head;
                }
                int m = n.array.Length;

                for (;;)
                {
                    long r = Volatile.Read(ref ps.requested);

                    while (e != r)
                    {
                        if (ps.IsCancelled)
                        {
                            ps.node = Dead;
                            return;
                        }
                        bool d = Volatile.Read(ref done);
                        bool empty = Volatile.Read(ref size) == e;

                        if (d && empty)
                        {
                            Exception ex = error;
                            if (ex != null)
                            {
                                ps.OnError(ex);
                            }
                            else
                            {
                                ps.OnComplete();
                            }
                            return;
                        }

                        if (empty)
                        {
                            break;
                        }

                        if (offset == m)
                        {
                            offset = 0;
                            n = n.next;
                        }

                        T v = n.array[offset];

                        ps.OnNext(v);

                        e++;
                        offset++;
                    }

                    if (e == r)
                    {
                        if (ps.IsCancelled)
                        {
                            return;
                        }

                        if (Volatile.Read(ref done) && Volatile.Read(ref size) == e)
                        {
                            Exception ex = error;
                            if (ex != null)
                            {
                                ps.OnError(ex);
                            }
                            else
                            {
                                ps.OnComplete();
                            }
                            return;
                        }
                    }

                    int w = Volatile.Read(ref ps.wip);
                    if (w == missed)
                    {
                        ps.emitted = e;
                        ps.offset = offset;
                        ps.node = n;
                        missed = Interlocked.Add(ref ps.wip, -missed);
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

            public override void ReplayFused(ProcessorSubscription ps)
            {
                int missed = 1;
                ArrayNode n = ps.node as ArrayNode;

                if (n == null)
                {
                    n = head;
                    ps.node = n;
                }

                for (;;)
                {
                    if (ps.IsCancelled)
                    {
                        ps.node = Dead;
                        return;
                    }
                    bool d = Volatile.Read(ref done);
                    bool empty = Volatile.Read(ref size) == ps.emitted;

                    ps.OnNext(default(T));

                    if (d && empty)
                    {
                        Exception ex = error;
                        if (ex != null)
                        {
                            ps.OnError(ex);
                        }
                        else
                        {
                            ps.OnComplete();
                        }
                        return;
                    }

                    int w = Volatile.Read(ref ps.wip);
                    if (w == missed)
                    {
                        missed = Interlocked.Add(ref ps.wip, -missed);
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

            static readonly Node Dead = new Node(default(T));

            Node head;

            Node tail;

            internal SizeBoundBufferManager(int maxSize)
            {
                this.maxSize = maxSize;
                var n = new Node(default(T));
                Volatile.Write(ref head, n);
                Volatile.Write(ref tail, n);
            }

            public override object DeadNode => Dead;

            public override void AddElement(T element)
            {
                Node n = new Node(element);
                var t = tail;
                Volatile.Write(ref t.next, n);
                tail = n;

                int s = size;
                if (size == maxSize)
                {
                    Volatile.Write(ref head, head.next);
                }
                else
                {
                    size = s + 1;
                }
            }

            public override void Finish()
            {
                // nothing to do
            }

            public override bool IsEmpty(ProcessorSubscription ps)
            {
                var n = ps.node as Node;
                return n == null || Volatile.Read(ref n.next) == null;
            }

            public override bool Poll(ProcessorSubscription ps, out T item)
            {
                if (ps.node is Node n)
                {
                    var next = Volatile.Read(ref n.next);
                    if (next != null)
                    {
                        item = next.item;
                        ps.node = next;
                        return true;
                    }
                }
                item = default(T);
                return false;
            }

            public override void ReplayAsync(ProcessorSubscription ps)
            {
                int missed = 1;
                long e = ps.emitted;
                var n = ps.node as Node;
                if (n == null)
                {
                    n = Volatile.Read(ref head);
                }

                for (;;)
                {
                    long r = Volatile.Read(ref ps.requested);

                    while (e != r)
                    {
                        if (ps.IsCancelled)
                        {
                            ps.node = Dead;
                            return;
                        }

                        bool d = Volatile.Read(ref done);
                        var next = Volatile.Read(ref n.next);
                        bool empty = next == null;

                        if (d && empty)
                        {
                            Exception ex = error;
                            if (ex != null)
                            {
                                ps.OnError(ex);
                            }
                            else
                            {
                                ps.OnComplete();
                            }
                            return;
                        }

                        if (empty)
                        {
                            break;
                        }

                        ps.OnNext(next.item);

                        e++;
                        n = next;
                    }

                    if (e == r)
                    {
                        if (ps.IsCancelled)
                        {
                            ps.node = Dead;
                            return;
                        }
                        if (Volatile.Read(ref done) && Volatile.Read(ref n.next) == null)
                        {
                            Exception ex = error;
                            if (ex != null)
                            {
                                ps.OnError(ex);
                            }
                            else
                            {
                                ps.OnComplete();
                            }
                            return;
                        }
                    }

                    int w = Volatile.Read(ref ps.wip);
                    if (w == missed)
                    {
                        ps.emitted = e;
                        ps.node = n;
                        missed = Interlocked.Add(ref ps.wip, -missed);
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

            public override void ReplayFused(ProcessorSubscription ps)
            {
                int missed = 1;
                var n = ps.node as Node;
                if (n == null)
                {
                    n = Volatile.Read(ref head);
                    ps.node = n;
                }

                for (;;)
                {
                    if (ps.IsCancelled)
                    {
                        return;
                    }
                    bool d = Volatile.Read(ref done);
                    bool empty = Volatile.Read(ref size) == ps.emitted;

                    ps.OnNext(default(T));

                    if (d && empty)
                    {
                        Exception ex = error;
                        if (ex != null)
                        {
                            ps.OnError(ex);
                        }
                        else
                        {
                            ps.OnComplete();
                        }
                        return;
                    }

                    int w = Volatile.Read(ref ps.wip);
                    if (w == missed)
                    {
                        missed = Interlocked.Add(ref ps.wip, -missed);
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

            static readonly TimedNode Dead = new TimedNode(default(T), long.MaxValue);

            TimedNode head;

            TimedNode tail;

            internal TimeBoundBufferManager(int maxSize, TimeSpan maxAge, IExecutorService executor)
            {
                this.maxSize = maxSize;
                this.maxAgeMillis = (long)maxAge.TotalMilliseconds;
                this.executor = executor;
                var n = new TimedNode(default(T), long.MinValue);
                tail = n;
                Volatile.Write(ref head, n);
            }

            public override object DeadNode => Dead;

            public override void AddElement(T element)
            {
                var now = executor.Now;
                var n = new TimedNode(element, now);
                var t = tail;
                Volatile.Write(ref t.next, n);
                tail = n;

                var h0 = head;
                var h = h0;
                int s = size;
                if (s == maxSize)
                {
                    h = h.next;
                }
                now -= maxAgeMillis;
                for (;;)
                {
                    n = Volatile.Read(ref h.next);
                    if (n == null)
                    {
                        break;
                    }
                    if (n.timestamp > now)
                    {
                        break;
                    }
                    h = n;
                    s--;
                }
                size = s;
                if (h0 != h)
                {
                    Volatile.Write(ref head, h);
                }
            }

    public override void Finish()
            {
                long now = executor.Now - maxAgeMillis;
                var h = head;
                for (;;)
                {
                    var n = Volatile.Read(ref h.next);
                    if (n == null)
                    {
                        break;
                    }
                    if (n.timestamp > now)
                    {
                        break;
                    }
                    h = n;
                }
                Volatile.Write(ref head, h);
            }

            TimedNode FindHead()
            {
                var h0 = Volatile.Read(ref head);
                var h = h0;
                long now = executor.Now - maxAgeMillis;
                for (;;)
                {
                    var n = Volatile.Read(ref h.next);
                    if (n == null)
                    {
                        break;
                    }
                    if (n.timestamp > now)
                    {
                        break;
                    }
                    h = n;
                }
                if (h != h0)
                {
                    Interlocked.CompareExchange(ref head, h, h0);
                }
                return h;
            }

            public override bool IsEmpty(ProcessorSubscription ps)
            {
                var n = ps.node as TimedNode;
                return n == null || Volatile.Read(ref n.next) == null;
            }

            public override bool Poll(ProcessorSubscription ps, out T item)
            {
                if (ps.node is TimedNode n)
                {
                    var next = Volatile.Read(ref n.next);
                    if (next != null)
                    {
                        item = next.item;
                        ps.node = next;
                        return true;
                    }
                }
                item = default(T);
                return false;
            }

            public override void ReplayAsync(ProcessorSubscription ps)
            {
                int missed = 1;
                long e = ps.emitted;
                var n = ps.node as TimedNode;
                if (n == null)
                {
                    n = FindHead();
                }

                for (;;)
                {
                    long r = Volatile.Read(ref ps.requested);

                    while (e != r)
                    {
                        if (ps.IsCancelled)
                        {
                            ps.node = Dead;
                            return;
                        }

                        bool d = Volatile.Read(ref done);
                        var next = Volatile.Read(ref n.next);
                        bool empty = next == null;

                        if (d && empty)
                        {
                            Exception ex = error;
                            if (ex != null)
                            {
                                ps.OnError(ex);
                            }
                            else
                            {
                                ps.OnComplete();
                            }
                            return;
                        }

                        if (empty)
                        {
                            break;
                        }

                        ps.OnNext(next.item);

                        e++;
                        n = next;
                    }

                    if (e == r)
                    {
                        if (ps.IsCancelled)
                        {
                            ps.node = Dead;
                            return;
                        }
                        if (Volatile.Read(ref done) && Volatile.Read(ref n.next) == null)
                        {
                            Exception ex = error;
                            if (ex != null)
                            {
                                ps.OnError(ex);
                            }
                            else
                            {
                                ps.OnComplete();
                            }
                            return;
                        }
                    }

                    int w = Volatile.Read(ref ps.wip);
                    if (w == missed)
                    {
                        ps.emitted = e;
                        ps.node = n;
                        missed = Interlocked.Add(ref ps.wip, -missed);
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

            public override void ReplayFused(ProcessorSubscription ps)
            {
                int missed = 1;
                var n = ps.node as TimedNode;
                if (n == null)
                {
                    n = FindHead();
                    ps.node = n;
                }

                for (;;)
                {
                    if (ps.IsCancelled)
                    {
                        return;
                    }
                    bool d = Volatile.Read(ref done);
                    bool empty = Volatile.Read(ref size) == ps.emitted;

                    ps.OnNext(default(T));

                    if (d && empty)
                    {
                        Exception ex = error;
                        if (ex != null)
                        {
                            ps.OnError(ex);
                        }
                        else
                        {
                            ps.OnComplete();
                        }
                        return;
                    }

                    int w = Volatile.Read(ref ps.wip);
                    if (w == missed)
                    {
                        missed = Interlocked.Add(ref ps.wip, -missed);
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
