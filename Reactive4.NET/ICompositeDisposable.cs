using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reactive4.NET
{
    /// <summary>
    /// Represents the API for a IDisposable container.
    /// </summary>
    public interface ICompositeDisposable : IDisposable
    {
        /// <summary>
        /// Add the specified IDisposable to this container.
        /// </summary>
        /// <param name="d">The IDisposable to add, not null</param>
        /// <returns>True if successful; false if the container has been disposed.</returns>
        bool Add(IDisposable d);

        /// <summary>
        /// Removes the specified IDisposable from this container
        /// and disposes it.
        /// </summary>
        /// <param name="d">The IDisposable to remove.</param>
        /// <returns>True if successful, false if the IDisposable was not in the container.</returns>
        bool Remove(IDisposable d);

        /// <summary>
        /// Removes the specified IDisposable from this container
        /// but does not dispose it.
        /// </summary>
        /// <param name="d">The IDisposable to delete.</param>
        /// <returns>True if successful, false if the IDisposable was not in the container.</returns>
        bool Delete(IDisposable d);

        /// <summary>
        /// Removes all current IDisposables from this container and
        /// disposes all of them.
        /// </summary>
        void Clear();

        /// <summary>
        /// Returns true if this container has been disposed.
        /// </summary>
        bool IsDisposed { get; }
    }
}
