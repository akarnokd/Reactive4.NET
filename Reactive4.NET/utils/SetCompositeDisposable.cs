using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Reactive4.NET.utils
{
    /// <summary>
    /// A container for IDisposables based on a backing HashSet.
    /// </summary>
    public sealed class SetCompositeDisposable : ICompositeDisposable
    {

        HashSet<IDisposable> set;

        int disposed;

        /// <summary>
        /// Constructs an empty SetCompositeDisposable.
        /// </summary>
        public SetCompositeDisposable()
        {
        }

        /// <summary>
        /// Constructs a SetCompositeDisposable with the given params array
        /// of initial IDisposables.
        /// </summary>
        /// <param name="disposables">The params array of IDisposables to start with.</param>
        public SetCompositeDisposable(params IDisposable[] disposables)
        {
            this.set = new HashSet<IDisposable>(disposables);
        }

        /// <summary>
        /// Constructs a SetCompositeDisposable with the given enumerable
        /// of initial IDisposables.
        /// </summary>
        /// <param name="disposables">The enumerable of IDisposables to start with.</param>
        public SetCompositeDisposable(IEnumerable<IDisposable> disposables)
        {
            this.set = new HashSet<IDisposable>(disposables);
        }

        /// <summary>
        /// Add the specified IDisposable to this container.
        /// </summary>
        /// <param name="d">The IDisposable to add, not null</param>
        /// <returns>True if successful; false if the container has been disposed.</returns>
        public bool Add(IDisposable d)
        {
            if (!IsDisposed)
            {
                lock (this)
                {
                    if (!IsDisposed)
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

        /// <summary>
        /// Removes all current IDisposables from this container and
        /// disposes all of them.
        /// </summary>
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

        /// <summary>
        /// Removes the specified IDisposable from this container
        /// but does not dispose it.
        /// </summary>
        /// <param name="d">The IDisposable to delete.</param>
        /// <returns>True if successful, false if the IDisposable was not in the container.</returns>
        public bool Delete(IDisposable d)
        {
            if (!IsDisposed)
            {
                lock (this)
                {
                    var s = set;
                    return s != null && s.Remove(d);
                }
            }
            return false;
        }

        /// <summary>
        /// Disposes this container and all IDisposable items it contains.
        /// </summary>
        public void Dispose()
        {
            if (!IsDisposed)
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

        /// <summary>
        /// Returns true if this container has been disposed.
        /// </summary>
        public bool IsDisposed => Volatile.Read(ref disposed) != 0;

        /// <summary>
        /// Removes the specified IDisposable from this container
        /// and disposes it.
        /// </summary>
        /// <param name="d">The IDisposable to remove.</param>
        /// <returns>True if successful, false if the IDisposable was not in the container.</returns>
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
