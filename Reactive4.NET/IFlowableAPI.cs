using System;
using System.Collections.Generic;
using System.Text;
using Reactive.Streams;

namespace Reactive4.NET
{
    /// <summary>
    /// Represents a relaxed ISubscriber that tells the IFlowable
    /// to run in relaxed mode.
    /// Relaxed mode means that the implementor of this interface
    /// ensures no non-positive request is issued and its
    /// OnSubscribe is thread safe if it calls Request().
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    public interface IFlowableSubscriber<in T> : ISubscriber<T>
    {
    }

    /// <summary>
    /// Extends the IFlowableSubscriber with a conditional TryOnNext()
    /// method that indicates the item was not consumed and a new
    /// item may be sent immediately.
    /// </summary>
    /// <remarks>This type of ISubscriber reduces the overhead coming
    /// from filter-like operators that call Request(1) when they 
    /// don't produce an item for an item.</remarks>
    /// <typeparam name="T">The value type.</typeparam>
    public interface IConditionalSubscriber<in T> : IFlowableSubscriber<T>
    {
        /// <summary>
        /// Tries to consume the item and returns true if successful;
        /// false indicates the upstream can send a new item without
        /// calling Request(1).
        /// </summary>
        /// <param name="item">The item to consume.</param>
        /// <returns>If true, the item was consumed; if false, 
        /// a new replacement item can be sent immediately without
        /// the need to call Request(1).</returns>
        bool TryOnNext(T item);
    }

    /// <summary>
    /// Marker interface indicating the IFlowable can produce
    /// a single (or no) item synchronously at subscription time,
    /// enabling optimizations.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    public interface IVariableSource<T>
    {
        /// <summary>
        /// Retrieves the single item from this source or
        /// returns false for an empty source.
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        bool Value(out T value);
    }

    /// <summary>
    /// Marker internface indicating the IFlowable can
    /// produce a single (or no) item synchronously at
    /// assembly time, enabling optimizations.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    public interface IConstantSource<T> : IVariableSource<T>
    {
    }

    /// <summary>
    /// A generic and simple queue interface with a minimal set
    /// of operations.
    /// </summary>
    /// <typeparam name="T">The element type of the queue.</typeparam>
    public interface ISimpleQueue<T>
    {
        /// <summary>
        /// Offer the item into this queue or return false
        /// if the queue is full.
        /// </summary>
        /// <param name="item">The item to offer.</param>
        /// <returns>True if successful; false if the queue is full.</returns>
        bool Offer(T item);

        /// <summary>
        /// Poll an item from this queue and return true if
        /// successful, false if the queue is empty or
        /// throws an exception if there was an error
        /// in a potential post-processing of the item
        /// in a fused scenario.
        /// </summary>
        /// <remarks>Note that IsEmpty() may return false indicating
        /// a non-empty queue but Poll() returning false indicating
        /// an empty queue. The reason for this is that
        /// certain post-processing may drop items in a fused
        /// scenario.</remarks>
        /// <param name="item">The item polled.</param>
        /// <returns>True if successful; false if the queue is empty.</returns>
        bool Poll(out T item);

        /// <summary>
        /// Returns true if the queue is empty or
        /// false if the queue is physically non-empty
        /// (but may be logically empty if Poll() is called).
        /// </summary>
        /// <returns>True if the queue is empty, false if it is
        /// likely non-empty.</returns>
        bool IsEmpty();

        /// <summary>
        /// Clears the contents of the queue.
        /// </summary>
        void Clear();
    }

    /// <summary>
    /// Represents an ISubscription and a ISimpleQueue to indicate
    /// the upstream is ready for operator micro-fusion.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    public interface IQueueSubscription<T> : ISubscription, ISimpleQueue<T>
    {
        /// <summary>
        /// Request a fusion mode, see the FusionSupport constants.
        /// </summary>
        /// <param name="mode">The fusion mode requested. See the FusionSupport constants.</param>
        /// <returns>The established fusion mode: NONE, SYNC or ASYNC.</returns>
        int RequestFusion(int mode);
    }

    /// <summary>
    /// Represents a reactive base interface for this library that
    /// extends the Reactive-Streams IPublisher and accepts
    /// the relaxed IFlowableSubscribers as consumers.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    public interface IFlowable<out T> : IPublisher<T>
    {
        /// <summary>
        /// Subscribe with the relaxed IFlowableSubscriber instance.
        /// </summary>
        /// <param name="subscriber">The IFlowableSubscriber instance, not null.</param>
        void Subscribe(IFlowableSubscriber<T> subscriber);
    }

    /// <summary>
    /// Marker interface to indicate an IFlowable operator
    /// has an upstream source assembled.
    /// </summary>
    /// <typeparam name="T">The element type of the upstream.</typeparam>
    public interface IHasSource<T>
    {
        /// <summary>
        /// Returns the upstream source.
        /// </summary>
        IFlowable<T> Source { get; }
    }

    /// <summary>
    /// Marker interface that indicates the operator can
    /// return a new instance of itself with the same parameters
    /// but different upstream source.
    /// </summary>
    /// <typeparam name="T">The element type of the upstream.</typeparam>
    /// <typeparam name="F">The result IFlowable type.</typeparam>
    /// <typeparam name="R">The result type of the IFlowable returned.</typeparam>
    public interface IReplaceSource<T, R, F> where R : IFlowable<R>
    {
        /// <summary>
        /// Create a new instance of the operator with
        /// a new upstream source
        /// </summary>
        /// <param name="newSource">The new upstream source to use</param>
        /// <returns>The new instance of the operator.</returns>
        F WithSource(IFlowable<T> newSource);
    }

    /// <summary>
    /// Interface combining the same-type IProcessor interface with
    /// the IFlowable (to support the relaxed IFlowableSubscriber),
    /// IDisposable (to support upstream cancellation) and
    /// being IFlowableSubscriber (to support relaxed consumption of
    /// other IFlowables).
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    public interface IFlowableProcessor<T> : IFlowable<T>, IProcessor<T, T>, IFlowableSubscriber<T>, IDisposable
    {
        /// <summary>
        /// Indicates that this IFlowableProcessor has completed normally.
        /// </summary>
        bool HasComplete { get; }

        /// <summary>
        /// Indicates that this IFlowableProcessor has terminated with an exception.
        /// </summary>
        bool HasException { get; }

        /// <summary>
        /// Returns the terminal exception if HasException is true, null otherwise.
        /// </summary>
        Exception Exception { get; }

        /// <summary>
        /// Indicates there are any subscribers subscribed to this IFlowableProcessor.
        /// </summary>
        bool HasSubscribers { get; }
    }

    /// <summary>
    /// Constants for IQueueSubscription.RequestFusion mode and
    /// return values.
    /// </summary>
    public static class FusionSupport
    {
        /// <summary>
        /// Returned by RequestFusion indicating no fusion happens.
        /// </summary>
        public static readonly int NONE = 0;
        /// <summary>
        /// Provided as input and returned by RequestFusion indicating
        /// a synchronous fusion mode. In synchronous fusion,
        /// Request() should never be called on the ISubscription.
        /// </summary>
        public static readonly int SYNC = 1;
        /// <summary>
        /// Provided as input and returned by RequestFusion indicating
        /// a synchronous fusion mode.
        /// </summary>
        public static readonly int ASYNC = 2;
        /// <summary>
        /// Provided as input to RequestFusion indicating
        /// a request for synchronous or asynchronous fusion mode.
        /// </summary>
        public static readonly int ANY = SYNC | ASYNC;
        /// <summary>
        /// Provided as input logically OR-ed with any of the other
        /// input constants to indicate the requestor acts as
        /// an asynchronous boundary and the Poll() side of the
        /// queue where the fused side-effects would happen
        /// is over the asynchronous boundary and may execute
        /// these side effects on an undesired thread.
        /// </summary>
        public static readonly int BARRIER = 4;
    }

    /// <summary>
    /// Represents an IFlowable with an associated key.
    /// </summary>
    /// <typeparam name="K">The key type.</typeparam>
    /// <typeparam name="V">The value type.</typeparam>
    public interface IGroupedFlowable<K, V> : IFlowable<V>
    {
        /// <summary>
        /// The group key.
        /// </summary>
        K Key { get; }
    }

    /// <summary>
    /// Represents an IFlowable that can be manually
    /// connected to an upstream source.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    public interface IConnectableFlowable<T> : IFlowable<T>
    {
        /// <summary>
        /// Connect this IConnectableFlowable to its upstream
        /// if not already having an active, non-terminated connection.
        /// </summary>
        /// <param name="onConnect">The optional action called with an
        /// IDisposable that allows disconnecting from the upstream.</param>
        /// <returns></returns>
        IDisposable Connect(Action<IDisposable> onConnect = null);

        /// <summary>
        /// Resets the state of the IConnectableFlowable after it
        /// reached a terminal state and allows a new set
        /// of ISubscribers to be pre-subscribed just like when
        /// the IConnectableObservable was fresh.
        /// </summary>
        void Reset();
    }
}
