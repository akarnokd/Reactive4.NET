using System;
using System.Collections.Generic;
using System.Text;

namespace Reactive4.NET
{
    /// <summary>
    /// API for emitting events in an imperative fasion via Flowable.Create()
    /// and have additional safeguards and resource management.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    public interface IFlowableEmitter<T> : IGeneratorEmitter<T>
    {
        /// <summary>
        /// Register an action to be called when the sequence 
        /// gets terminated via OnError or OnComplete or
        /// cancellation from downstream.
        /// </summary>
        /// <remarks>Only one Action or IDisposable is supported,
        /// calling this method again disposes the previously
        /// registered action or disposable.</remarks>
        /// <param name="action">The action to execute.</param>
        void OnCancel(Action action);

        /// <summary>
        /// Register an IDisposable to be called when the sequence 
        /// gets terminated via OnError or OnComplete or
        /// cancellation from downstream.
        /// </summary>
        /// <remarks>Only one Action or IDisposable is supported,
        /// calling this method again disposes the previously
        /// registered disposable or action.</remarks>
        /// <param name="disposable">The IDisposable to dispose.</param>
        void OnCancel(IDisposable disposable);

        /// <summary>
        /// Returns the current total requested amount.
        /// </summary>
        /// <remarks>Emitting items won't decrease this amount
        /// and the emission count should be tracked separately.</remarks>
        long Requested { get; }

        /// <summary>
        /// Returns true if the downstream cancelled the sequence.
        /// </summary>
        bool IsCancelled { get; }
    }
}
