using System;
using System.Collections.Generic;
using System.Text;

namespace Reactive4.NET
{
    /// <summary>
    /// An abstraction over an ISubscriber with only the OnNext, OnError and
    /// OnComplete methods to support imperative emission of these signals.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    public interface IGeneratorEmitter<in T>
    {
        /// <summary>
        /// Signal an item to downstream.
        /// </summary>
        /// <remarks>If the sequence is terminated the item is not emitted.</remarks>
        /// <param name="item">The item to signal.</param>
        void OnNext(T item);

        /// <summary>
        /// Signal an error to the downstream
        /// </summary>
        /// <param name="error">The error to signal.</param>
        /// <remarks>If the sequence is terminated the error is not emitted.</remarks>
        void OnError(Exception error);

        /// <summary>
        /// Signal a completion to the downstream.
        /// </summary>
        /// <remarks>If the sequence is terminated the call has no effect.</remarks>
        void OnComplete();
    }
}
