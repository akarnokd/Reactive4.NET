using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Reactive4.NET.utils
{
    public sealed class SetCompositeDisposable : ICompositeDisposable
    {

        HashSet<IDisposable> set;

        int disposed;

        public SetCompositeDisposable()
        {
        }

        public SetCompositeDisposable(IEnumerable<IDisposable> input)
        {
            this.set = new HashSet<IDisposable>(input);
        }

        public bool Add(IDisposable d)
        {
            if (!IsDisposed())
            {
                lock (this)
                {
                    if (!IsDisposed())
                    {
                        var s = set;
                        if (s == null)
                        {
                            s = new HashSet<IDisposable>();
                            set = s;
                        }
                        s.Add(d);
                        return true;
                    }
                }
            }
            d?.Dispose();
            return false;
        }

        public void Clear()
        {
            HashSet<IDisposable> s;
            lock (this)
            {
                s = set;
                set = null;
            }

            if (s != null)
            {
                foreach (var d in s) {
                    d.Dispose();
                }
            }
        }

        public bool Delete(IDisposable d)
        {
            if (!IsDisposed())
            {
                lock (this)
                {
                    var s = set;
                    return s != null && s.Remove(d);
                }
            }
            return false;
        }

        public void Dispose()
        {
            if (!IsDisposed())
            {
                Interlocked.Exchange(ref disposed, 1);
                HashSet<IDisposable> s;
                lock (this)
                {
                    s = set;
                    set = null;
                }

                if (s != null)
                {
                    foreach (var d in s)
                    {
                        d.Dispose();
                    }
                }
            }
        }

        public bool IsDisposed()
        {
            return Volatile.Read(ref disposed) != 0;
        }

        public bool Remove(IDisposable d)
        {
            if (Delete(d))
            {
                d.Dispose();
                return true;
            }
            return false;
        }
    }
}
