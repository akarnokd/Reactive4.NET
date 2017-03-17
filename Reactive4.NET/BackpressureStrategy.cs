using System;
using System.Collections.Generic;
using System.Text;

namespace Reactive4.NET
{
    /// <summary>
    /// Enumeration for backpressure strategies in certain
    /// operators.
    /// </summary>
    public enum BackpressureStrategy
    {
        /// <summary>
        /// No backpressure is applied and unless
        /// one of the OnBackpressureX operators is present,
        /// this may lead to undefined behavior or dataloss.
        /// </summary>
        MISSING,
        /// <summary>
        /// Signal an OnError when the downstream can't keep up.
        /// </summary>
        ERROR,
        /// <summary>
        /// Drop the item when the downstream can't keep up.
        /// </summary>
        DROP,
        /// <summary>
        /// Keep and overwrite the latest item when the downstream
        /// can't keep up.
        /// </summary>
        LATEST,
        /// <summary>
        /// Buffer items (possibly in an unbounded manner)
        /// until the dowstream is ready to receive them.
        /// </summary>
        BUFFER
    }
}
