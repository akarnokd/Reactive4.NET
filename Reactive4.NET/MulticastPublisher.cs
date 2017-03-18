using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Reactive.Streams;
using Reactive4.NET.operators;

namespace Reactive4.NET
{
    /// <summary>
    /// A multicasting publisher that dispatches events to subscribers
    /// on their own IExecutorWorkers.
    /// </summary>
    /// <typeparam name="T">The input and output value type.</typeparam>
    public sealed class MulticastPublisher<T> : IFlowable<T>, IDisposable
    {
        /// <summary>
        /// Indicates that this MulticastPublisher was completed normally.
        /// </summary>
        public bool HasComplete => throw new NotImplementedException();

        /// <summary>
        /// Indicates that this MulticastPublisher was terminated with an exception.
        /// </summary>
        public bool HasException => throw new NotImplementedException();

        /// <summary>
        /// Returns the terminal exception if HasException is true, null otherwise.
        /// </summary>
        public Exception Exception => throw new NotImplementedException();

        /// <summary>
        /// Indicates there are any subscribers subscribed to this MulticastPublisher.
        /// </summary>
        public bool HasSubscribers => throw new NotImplementedException();

        readonly IExecutorService executor;

        readonly int bufferSize;

        /// <summary>
        /// Constructs a MulticastPublisher with the Executors.Task service and
        /// default buffer size.
        /// </summary>
        public MulticastPublisher() : this(Executors.Task, Flowable.BufferSize())
        {
        }

        /// <summary>
        /// Constructs a MulticastPublisher with the given executor service
        /// and default buffer size.
        /// </summary>
        /// <param name="executor">The IExecutorService to use.</param>
        public MulticastPublisher(IExecutorService executor) : this(executor, Flowable.BufferSize())
        {
        }

        /// <summary>
        /// Constructs a MulticastPublisher with the given executor service
        /// and given per-subscriber buffer size.
        /// </summary>
        /// <param name="executor">The IExecutorService to use.</param>
        /// <param name="bufferSize">The buffer size per-subscriber, positive.</param>
        public MulticastPublisher(IExecutorService executor, int bufferSize)
        {
            this.executor = executor;
            this.bufferSize = bufferSize;
        }

        /// <summary>
        /// Subscribe with the relaxed IFlowableSubscriber instance.
        /// </summary>
        /// <param name="subscriber">The IFlowableSubscriber instance, not null.</param>
        public void Subscribe(IFlowableSubscriber<T> subscriber)
        {
        }

        /// <summary>
        /// Request IPublisher to start streaming data.
        /// This is a "factory method" and can be called multiple times, each time starting
        /// a new ISubscription.
        /// Each ISubscription will work for only a single ISubscriber.
        /// A ISubscriber should only subscribe once to a single IPublisher.
        /// If IPublisher rejects the subscription attempt or otherwise
        /// fails it will signal the error via ISubscriber.OnError(Exception).
        /// </summary>
        /// <param name="subscriber">The ISubscriber that will consume signals from this IPublisher</param>
        public void Subscribe(ISubscriber<T> subscriber)
        {
            if (subscriber == null)
            {
                throw new ArgumentNullException(nameof(subscriber));
            }
            if (subscriber is IFlowableSubscriber<T> s)
            {
                Subscribe(s);
            }
            else
            {
                Subscribe(new StrictSubscriber<T>(subscriber));
            }
        }

        /// <summary>
        /// Tries to offer an item to all current subscribers or
        /// returns false if all of them have their buffer filled up.
        /// </summary>
        /// <param name="element">The item to offer to all current subscribers.</param>
        /// <returns>True if successful; false if all current subscribers have their
        /// individual buffer filled up.</returns>
        public bool Offer(T element)
        {

            return true;
        }

        /// <summary>
        /// Changes the MulticastPublisher into a terminal state and
        /// completes all current and future ISubscribers.
        /// </summary>
        public void Dispose()
        {

        }

        /// <summary>
        /// Changes the MulticastPublisher into a terminal state and
        /// signals an error to all current and future ISubscribers.
        /// </summary>
        /// <param name="ex">The exception to signal, not null.</param>
        public void Fail(Exception ex)
        {

        }
    }
}
