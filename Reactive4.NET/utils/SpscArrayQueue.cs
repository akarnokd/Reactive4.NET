using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Reactive4.NET.utils
{
    internal sealed class SpscArrayQueue<T> : ISimpleQueue<T>
    {
        readonly Entry[] array;

        long producerIndex;

        long consumerIndex;

        internal SpscArrayQueue(int capacity)
        {
            this.array = new Entry[capacity];
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
            Entry[] a = array;
            int m = a.Length - 1;
            long pi = producerIndex;
            int offset = (int)pi & m;

            if (Volatile.Read(ref a[offset].state) != 0)
            {
                return false;
            }
            a[offset].element = item;
            Volatile.Write(ref a[offset].state, 1);
            Volatile.Write(ref producerIndex, pi + 1);
            return true;
        }

        public bool Poll(out T item)
        {
            Entry[] a = array;
            int m = a.Length - 1;
            long ci = consumerIndex;
            int offset = (int)ci & m;
            if (Volatile.Read(ref a[offset].state) != 0)
            {
                item = a[offset].element;
                a[offset].element = default(T);
                Volatile.Write(ref a[offset].state, 0);
                Volatile.Write(ref consumerIndex, ci + 1);
                return true;
            }
            item = default(T);
            return false;
        }

        struct Entry
        {
            internal int state;
            internal T element;
        }
    }
}
