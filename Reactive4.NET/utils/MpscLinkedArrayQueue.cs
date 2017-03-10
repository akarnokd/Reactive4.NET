using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Reactive4.NET.utils
{
    sealed class MpscLinkedArrayQueue<T> : ISimpleQueue<T>
    {
        QueueSection consumerQueue;

        int consumerIndex;

        QueueSection producerQueue;

        public MpscLinkedArrayQueue(int capacity)
        {
            var q = new QueueSection(capacity);
            Volatile.Write(ref consumerQueue, q);
            Volatile.Write(ref producerQueue, q);
        }

        public void Clear()
        {
            while (Poll(out T item) && !IsEmpty()) ;
        }

        public bool IsEmpty()
        {
            var q = consumerQueue;
            return consumerIndex == Math.Min(q.array.Length, Volatile.Read(ref q.producerIndex)) && Volatile.Read(ref q.next) == null;
        }

        public bool Offer(T item)
        {
            var q = Volatile.Read(ref producerQueue);
            var a = q.array;
            var n = a.Length;

            for (;;)
            {
                int offset = Volatile.Read(ref q.producerIndex);
                if (offset < n)
                {
                    offset = Interlocked.Increment(ref q.producerIndex) - 1;
                }
                if (offset >= n)
                {
                    var next = Volatile.Read(ref q.next);
                    if (next != null)
                    {
                        Interlocked.CompareExchange(ref producerQueue, next, q);
                        q = next;
                        a = next.array;
                        continue;
                    }

                    next = new QueueSection(n);
                    a = next.array;
                    a[0].item = item;
                    a[0].state = 1;
                    next.producerIndex = 1;

                    if (Interlocked.CompareExchange(ref q.next, next, null) != null)
                    {
                        next = q.next;
                        Interlocked.CompareExchange(ref producerQueue, next, q);
                        q = next;
                        a = q.array;
                        continue;
                    }

                    Interlocked.CompareExchange(ref producerQueue, next, q);
                    return true;
                }
                a[offset].item = item;
                Volatile.Write(ref a[offset].state, 1);
                return true;
            }
        }

        public bool Poll(out T item)
        {
            var q = consumerQueue;
            var a = q.array;
            var n = a.Length;
            var ci = consumerIndex;

            for (;;)
            {
                if (ci == n)
                {
                    var q2 = Volatile.Read(ref q.next);
                    if (q2 == null)
                    {
                        item = default(T);
                        return false;
                    }
                    //q.next = null;
                    consumerQueue = q2;
                    a = q2.array;
                    ci = 0;
                }
                if (Volatile.Read(ref a[ci].state) != 0)
                {
                    item = a[ci].item;
                    a[ci].item = default(T);
                    consumerIndex = ci + 1;
                    return true;
                }

                if (ci == Math.Min(n, Volatile.Read(ref q.producerIndex)) && Volatile.Read(ref q.next) == null)
                {
                    item = default(T);
                    return false;
                }
            }
        }

        internal struct Entry
        {
            internal T item;
            internal int state;
        }

        sealed class QueueSection
        {
            internal readonly Entry[] array;

            internal QueueSection next;

            internal int producerIndex;

            internal QueueSection(int capacity)
            {
                this.array = new Entry[capacity];
            }
        }
    }
}
