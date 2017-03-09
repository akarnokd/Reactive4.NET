using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reactive4.NET.utils
{
    internal static class QueueHelper
    {
        internal static int Pow2(int v)
        {
            v--;
            v |= v >> 1;
            v |= v >> 2;
            v |= v >> 4;
            v |= v >> 8;
            v |= v >> 16;
            v++;
            return v;
        }

        internal static void Clear<T>(ISimpleQueue<T> q)
        {
            while (q.Poll(out T item) && !q.IsEmpty()) ;
        }
    }
}
