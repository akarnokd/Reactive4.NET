using Reactive.Streams;
using Reactive4.NET.operators;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reactive4.NET
{
    /// <summary>
    /// Extension methods to support the IParallelFlowable sequences.
    /// </summary>
    public static class ParallelFlowable
    {
        // ********************************************************************************
        // Instance operators
        // ********************************************************************************

        /// <summary>
        /// Creates an IParallelFlowable with parallelism equal to the number of
        /// available processors and default prefetch amount.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The source IFlowable instance.</param>
        /// <returns>The new IParallelFlowable instance.</returns>
        public static IParallelFlowable<T> Parallel<T>(this IFlowable<T> source)
        {
            return Parallel(source, Environment.ProcessorCount, Flowable.BufferSize());
        }

        /// <summary>
        /// Creates an IParallelFlowable with the provided parallelism
        /// and default prefetch amount.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The source IFlowable instance.</param>
        /// <param name="parallelism">The number of parallel 'rail's to create, positive.</param>
        /// <returns>The new IParallelFlowable instance.</returns>
        public static IParallelFlowable<T> Parallel<T>(this IFlowable<T> source, int parallelism)
        {
            return Parallel(source, parallelism, Flowable.BufferSize());
        }

        /// <summary>
        /// Creates an IParallelFlowable with the provided parallelism
        /// and prefetch/buffer amount.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The source IFlowable instance.</param>
        /// <param name="parallelism">The number of parallel 'rail's to create, positive.</param>
        /// <param name="bufferSize">The prefetch/buffer amount towards the downstream.</param>
        /// <returns>The new IParallelFlowable instance.</returns>
        public static IParallelFlowable<T> Parallel<T>(this IFlowable<T> source, int parallelism, int bufferSize)
        {
            return new ParallelFlowableFork<T>(source, parallelism, bufferSize);
        }

        /// <summary>
        /// Runs each rail on its own worker of the given IExecutorService, similar
        /// to how Flowable.observeOn operates.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The source IParallelFlowable instance.</param>
        /// <param name="executor">The IExecutorService that will provide the workers for each rail.</param>
        /// <returns>The new IParallelFlowable instance.</returns>
        public static IParallelFlowable<T> RunOn<T>(this IParallelFlowable<T> source, IExecutorService executor)
        {
            return RunOn(source, executor, Flowable.BufferSize());
        }

        /// <summary>
        /// Runs each rail on its own worker of the given IExecutorService, similar
        /// to how Flowable.observeOn operates.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The source IParallelFlowable instance.</param>
        /// <param name="executor">The IExecutorService that will provide the workers for each rail.</param>
        /// <param name="bufferSize">The number of items to prefetch and keep in the buffer while crossing
        /// the (async) boundary of the worker.</param>
        /// <returns>The new IParallelFlowable instance.</returns>
        public static IParallelFlowable<T> RunOn<T>(this IParallelFlowable<T> source, IExecutorService executor, int bufferSize)
        {
            return new ParallelFlowableRunOn<T>(source, executor, bufferSize);
        }

        /// <summary>
        /// Consumes all rails of the IParallelFlowable and serializes them back into a
        /// single sequential IFlowable in a round-robin fashion.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The source IParallelFlowable instance.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<T> Sequential<T>(this IParallelFlowable<T> source)
        {
            return Sequential(source, Flowable.BufferSize());
        }

        /// <summary>
        /// Consumes all rails of the IParallelFlowable and serializes them back into a
        /// single sequential IFlowable in a round-robin fashion.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The source IParallelFlowable instance.</param>
        /// <param name="prefetch">The prefetch amount and buffer size for consuming each rail.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<T> Sequential<T>(this IParallelFlowable<T> source, int prefetch)
        {
            return new ParallelFlowableJoin<T>(source, prefetch);
        }

        public static IParallelFlowable<R> Map<T, R>(this IParallelFlowable<T> source, Func<T, R> mapper)
        {
            // TODO implement
            throw new NotImplementedException();
        }

        public static IParallelFlowable<T> Filter<T>(this IParallelFlowable<T> source, Func<T, bool> predicate)
        {
            // TODO implement
            throw new NotImplementedException();
        }

        public static IParallelFlowable<T> DoOnNext<T>(this IParallelFlowable<T> source, Action<T> onNext)
        {
            // TODO implement
            throw new NotImplementedException();
        }

        public static IParallelFlowable<T> DoOnError<T>(this IParallelFlowable<T> source, Action<Exception> onError)
        {
            // TODO implement
            throw new NotImplementedException();
        }

        public static IParallelFlowable<T> DoOnComplete<T>(this IParallelFlowable<T> source, Action onComplete)
        {
            // TODO implement
            throw new NotImplementedException();
        }

        public static IParallelFlowable<T> DoFinally<T>(this IParallelFlowable<T> source, Action onFinally)
        {
            // TODO implement
            throw new NotImplementedException();
        }

        public static IParallelFlowable<T> Reduce<T>(this IParallelFlowable<T> source, Func<T, T, T> reducer)
        {
            // TODO implement
            throw new NotImplementedException();
        }

        public static IParallelFlowable<R> Reduce<T, R>(this IParallelFlowable<T> source, Func<R> initialSupplier, Func<R, T, R> reducer)
        {
            // TODO implement
            throw new NotImplementedException();
        }

        public static IFlowable<T> ReduceAll<T>(this IParallelFlowable<T> source, Func<T, T, T> reducer)
        {
            // TODO implement
            throw new NotImplementedException();
        }

        public static IParallelFlowable<R> Collect<T, R>(this IParallelFlowable<T> source, Func<R> initialSupplier, Action<R, T> collector)
        {
            // TODO implement
            throw new NotImplementedException();
        }

        /// <summary>
        /// Calls the given composer function with the source IParallelFlowable to
        /// return another IParallelFlowable instance.
        /// </summary>
        /// <typeparam name="T">The source value type.</typeparam>
        /// <typeparam name="R">The result value type.</typeparam>
        /// <param name="source">The source IParallelFlowable instance.</param>
        /// <param name="composer">The function called with the source IParallelFlowable
        /// and should return a IParallelFlowable with possibly different element type.</param>
        /// <returns>The IParallelFlowable instance returned by the composer function.</returns>
        public static IParallelFlowable<R> Compose<T, R>(this IParallelFlowable<T> source, Func<IParallelFlowable<T>, IParallelFlowable<R>> composer)
        {
            return To(source, composer);
        }

        /// <summary>
        /// Calls the given converter function in a fluent manner and returns its
        /// result.
        /// </summary>
        /// <typeparam name="T">The value type of the source.</typeparam>
        /// <typeparam name="R">The result type returned by teh converter.</typeparam>
        /// <param name="source">The source IParallelFlowable instance.</param>
        /// <param name="converter">The function receiving the source and retunrs
        /// a value to be returned.</param>
        /// <returns>The value returned by the converter function.</returns>
        public static R To<T, R>(this IParallelFlowable<T> source, Func<IParallelFlowable<T>, R> converter)
        {
            return converter(source);
        }

        public static IParallelFlowable<R> FlatMap<T, R>(this IParallelFlowable<T> source, Func<T, IPublisher<R>> mapper)
        {
            return FlatMap<T, R>(source, mapper, Flowable.BufferSize(), Flowable.BufferSize());
        }

        public static IParallelFlowable<R> FlatMap<T, R>(this IParallelFlowable<T> source, Func<T, IPublisher<R>> mapper, int maxConcurrency)
        {
            return FlatMap<T, R>(source, mapper, maxConcurrency, Flowable.BufferSize());
        }

        public static IParallelFlowable<R> FlatMap<T, R>(this IParallelFlowable<T> source, Func<T, IPublisher<R>> mapper, int maxConcurrency, int bufferSize)
        {
            // TODO implement
            throw new NotImplementedException();
        }

        public static IParallelFlowable<R> ConcatMap<T, R>(this IParallelFlowable<T> source, Func<T, IPublisher<R>> mapper)
        {
            return ConcatMap<T, R>(source, mapper, 2);
        }

        public static IParallelFlowable<R> ConcatMap<T, R>(this IParallelFlowable<T> source, Func<T, IPublisher<R>> mapper, int prefetch)
        {
            // TODO implement
            throw new NotImplementedException();
        }

        public static IFlowable<IList<T>> ToSortedList<T>(this IParallelFlowable<T> source)
        {
            return ToSortedList(source, Comparer<T>.Default);
        }

        public static IFlowable<IList<T>> ToSortedList<T>(this IParallelFlowable<T> source, IComparer<T> comparer)
        {
            // TODO implement
            throw new NotImplementedException();
        }

        public static IFlowable<T> Sorted<T>(this IParallelFlowable<T> source)
        {
            return Sorted(source, Comparer<T>.Default);
        }

        public static IFlowable<T> Sorted<T>(this IParallelFlowable<T> source, IComparer<T> comparer)
        {
            // TODO implement
            throw new NotImplementedException();
        }

        /// <summary>
        /// Turns the array of IPublishers into an IParallelFlowable where the number
        /// of rails is equal to the number of array items and each rail
        /// will subscribe to the specific IPublisher.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="sources">The params array of IPublishers to turn into an IParallelFlowable.</param>
        /// <returns>The new IParallelFlowable instance.</returns>
        public static IParallelFlowable<T> FromArray<T>(params IPublisher<T>[] sources) {
            return new ParallelFlowableArray<T>(sources);
        }
    }
}
