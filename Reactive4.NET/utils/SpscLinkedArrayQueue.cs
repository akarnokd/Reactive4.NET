using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Reactive4.NET.utils
{
    internal sealed class SpscLinkedArrayQueue<T> : ISimpleQueue<T>
    {
        QueueSection producerQueue;

        long producerIndex;

        QueueSection consumerQueue;

        long consumerIndex;

        public SpscLinkedArrayQueue(int bufferSize)
        {
            int c = QueueHelper.Pow2(Math.Max(2, bufferSize));
            var q = new QueueSection(c);
            producerQueue = q;
            Volatile.Write(ref consumerQueue, q);
        }

        public void Clear()
        {
            while (Poll(out T item) && !IsEmpty()) ;
        }

        public bool IsEmpty()
        {
            return Volatile.Read(ref producerIndex) == Volatile.Read(ref consumerIndex);
        }

        public bool Offer(T item)
        {
            var q = producerQueue;
            var a = q.entries;
            var m = a.Length - 1;
            var pi = producerIndex;
            var offsetNext = (int)(pi + 1) & m;
            var offset = (int)(pi) & m;

            if (Volatile.Read(ref a[offsetNext].state) != 0)
            {
                var q2 = new QueueSection(m + 1);
                var b = q2.entries;
                b[offset].value = item;
                b[offset].state = 1;
                q.next = q2;
                producerQueue = q2;
                Volatile.Write(ref a[offset].state, 2);
                Volatile.Write(ref producerIndex, pi + 1);
                return true;
            }
            a[offset].value = item;
            Volatile.Write(ref a[offset].state, 1);
            Volatile.Write(ref producerIndex, pi + 1);
            return true;
        }

        public bool Poll(out T item)
        {
            var q = consumerQueue;
            var a = q.entries;
            var m = a.Length - 1;
            var ci = consumerIndex;
            var offset = (int)(ci) & m;

            int s = Volatile.Read(ref a[offset].state);
            if (s == 0)
            {
                item = default(T);
                return false;
            }

            if (s == 2)
            {
                var q2 = q.next;
                a = q2.entries;
                q.next = null;
                consumerQueue = q2;
            }

            item = a[offset].value;
            Volatile.Write(ref a[offset].state, 0);
            Volatile.Write(ref consumerIndex, ci + 1);
            return true;
        }

        sealed class QueueSection
        {
            internal readonly Entry[] entries;

            internal QueueSection next;

            internal QueueSection(int bufferSize)
            {
                this.entries = new Entry[bufferSize];
            }
        }

        internal struct Entry
        {
            internal int state;
            internal T value;
        }
    }
}
