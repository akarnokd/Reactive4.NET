using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reactive4.NET.utils
{
    /// <summary>
    /// Synchronous queue that is based on an array of items that can grow.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    sealed class ArrayQueue<T> : ISimpleQueue<T>
    {
        readonly int initialSize;
        T[] array;

        long producerIndex;
        long consumerIndex;

        internal ArrayQueue() : this(8) { }

        internal ArrayQueue(int initialSize)
        {
            this.initialSize = QueueHelper.Pow2(initialSize);
            array = new T[this.initialSize];
        }

        public void Clear()
        {
            producerIndex = 0;
            consumerIndex = 0;
            array = new T[initialSize];
        }

        public bool IsEmpty()
        {
            return producerIndex == consumerIndex;
        }

        public bool Offer(T item)
        {
            var a = array;
            var n = a.Length;
            var pi = producerIndex;
            if (pi == consumerIndex + n)
            {
                T[] b = new T[n * 2];

                var co = (int)consumerIndex & (n - 1);
                Array.Copy(a, co, b, 0, n - co);
                Array.Copy(a, 0, b, n - co, co);
                b[n] = item;
                consumerIndex = 0;
                producerIndex = n + 1;
                array = b;
                return true;
            }
            var offset = (int)pi & (n - 1);
            a[offset] = item;
            producerIndex = pi + 1;
            return true;
        }
        
        public bool Poll(out T item)
        {
            var a = array;
            var n = a.Length;
            var ci = consumerIndex;
            if (ci == producerIndex)
            {
                item = default(T);
                return false;
            }
            var offset = (int)ci & (n - 1);
            item = a[offset];
            a[offset] = default(T);
            consumerIndex = ci + 1;
            return true;
        }

        public bool Peek(out T item)
        {
            var a = array;
            var n = a.Length;
            var ci = consumerIndex;
            if (ci == producerIndex)
            {
                item = default(T);
                return false;
            }
            var offset = (int)ci & (n - 1);
            item = a[offset];
            return true;
        }

        /// <summary>
        /// Polls for the element last offered.
        /// </summary>
        public bool PollLatestOffered(out T item)
        {
            var a = array;
            var n = a.Length;
            var pi = producerIndex;
            if (pi == consumerIndex)
            {
                item = default(T);
                return false;
            }
            var offset = (int)pi & (n - 1);
            item = a[offset];
            a[offset] = default(T);
            producerIndex = pi - 1;
            return true;
        }

        internal long Count => producerIndex - consumerIndex;

        internal void ForEach(Action<T> consumer)
        {
            var a = array;
            var m = a.Length - 1;
            var ci = consumerIndex;
            var pi = producerIndex;
            while (ci != pi)
            {
                int offset = (int)ci & m;
                consumer(a[offset]);
                ci++;
            }
        }

        internal void ForEach<S>(S state, Action<S, T> consumer)
        {
            var a = array;
            var m = a.Length - 1;
            var ci = consumerIndex;
            var pi = producerIndex;
            while (ci != pi)
            {
                int offset = (int)ci & m;
                consumer(state, a[offset]);
                ci++;
            }
        }
    }
}
