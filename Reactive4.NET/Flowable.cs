using Reactive4.NET.operators;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Reactive.Streams;
using Reactive4.NET.subscribers;
using System.Threading;
using Reactive4.NET.utils;

namespace Reactive4.NET
{
    /// <summary>
    /// Extension methods for composing backpressure-enabled operators in a fluent manner.
    /// </summary>
    public static class Flowable
    {
        /// <summary>
        /// The default buffer size, prefetch amount, capacity hint.
        /// </summary>
        /// <returns>The default buffer size, positive.</returns>
        public static int BufferSize()
        {
            return 128;
        }

        // ********************************************************************************
        // Interop methods
        // ********************************************************************************

        /// <summary>
        /// Converts an arbitrary Reactive-Streams IPublisher into an IFlowable to
        /// enable fluent API operations on it.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="publisher">The IPublisher to convert.</param>
        /// <returns>The new IFlowable instance</returns>
        public static IFlowable<T> ToFlowable<T>(this IPublisher<T> publisher)
        {
            if (publisher is IFlowable<T> f)
            {
                return f;
            }
            return new FlowableFromPublisher<T>(publisher);
        }

        /// <summary>
        /// Converts an arbitrary Reactive-Streams IPublisher into an IFlowable to
        /// enable fluent API operations on it.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="publisher">The IPublisher to convert.</param>
        /// <returns>The new IFlowable instance</returns>
        public static IFlowable<T> FromPublisher<T>(IPublisher<T> publisher)
        {
            return publisher.ToFlowable();
        }

        /// <summary>
        /// Convert a standard IObservable into an IFlowable that is run with the
        /// specified backpressure strategy.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The IObservable to convert.</param>
        /// <param name="strategy">The backpressure strategy to apply.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<T> ToFlowable<T>(this IObservable<T> source, BackpressureStrategy strategy)
        {
            return new FlowableFromObservable<T>(source, strategy);
        }

        /// <summary>
        /// Convert a standard IObservable into an IFlowable that is run with the
        /// specified backpressure strategy.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The IObservable to convert.</param>
        /// <param name="strategy">The backpressure strategy to apply.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<T> FromObservable<T>(IObservable<T> source, BackpressureStrategy strategy)
        {
            return source.ToFlowable(strategy);
        }

        /// <summary>
        /// Signals the success or failure of the given (ongoing) Task instance.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="task">The task whose outcome to signal.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<T> ToFlowable<T>(this Task<T> task)
        {
            return new FlowableFromTask<T>(task);
        }

        /// <summary>
        /// Signals the success or failure of the given (ongoing) Task instance.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="task">The task whose outcome to signal.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<T> FromTask<T>(Task<T> task)
        {
            return task.ToFlowable();
        }

        /// <summary>
        /// Signals the success (via an empty IFlowable) or failure of the given (ongoing) Task instance.
        /// </summary>
        /// <param name="task">The task whose outcome to signal.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<object> ToFlowable(this Task task)
        {
            return new FlowableFromTaskVoid(task);
        }

        /// <summary>
        /// Signals the success (via an empty IFlowable) or failure of the given (ongoing) Task instance.
        /// </summary>
        /// <param name="task">The task whose outcome to signal.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<object> FromTask(this Task task)
        {
            return task.ToFlowable();
        }

        /// <summary>
        /// Convert the IFlowable instance into a standard IObservable.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The source IFlowable to convert.</param>
        /// <returns>The new IObservable instance.</returns>
        public static IObservable<T> ToObservable<T>(this IFlowable<T> source)
        {
            return new FlowableToObservable<T>(source);
        }

        // ********************************************************************************
        // Factory methods
        // ********************************************************************************

        /// <summary>
        /// Signals a single item.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="item">The item to emit.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<T> Just<T>(T item)
        {
            return new FlowableJust<T>(item);
        }

        /// <summary>
        /// Repeatedly signals the given item indefinitely.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="item">The item to emit.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<T> RepeatItem<T>(T item)
        {
            return new FlowableRepeatItem<T>(item);
        }

        /// <summary>
        /// Signals the given exception instance.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="exception">The exception to signal.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<T> Error<T>(Exception exception)
        {
            return new FlowableError<T>(exception);
        }

        /// <summary>
        /// Signals the Exception returned by the errorSupplier for each
        /// individual ISubscriber.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="errorSupplier">The Func that returns the Exception to signal.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<T> Error<T>(Func<Exception> errorSupplier)
        {
            return new FlowableErrorSupplier<T>(errorSupplier);
        }

        /// <summary>
        /// Signals a normal completion.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<T> Empty<T>()
        {
            return FlowableEmpty<T>.Instance;
        }

        /// <summary>
        /// Doesn't signal anything.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<T> Never<T>()
        {
            return FlowableNever<T>.Instance;
        }

        /// <summary>
        /// Signals the only item returned by the function call.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="function">The function to call for an item.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<T> FromFunction<T>(Func<T> function)
        {
            return new FlowableFromFunction<T>(function);
        }

        /// <summary>
        /// Repeatedly calls a function and signals the value it returned.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="function">The function called repeatedly.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<T> RepeatFunction<T>(Func<T> function)
        {
            return new FlowableRepeatFunction<T>(function);
        }

        /// <summary>
        /// Creates an IFlowable and allows signalling events in an imperative
        /// push manner.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="emitter">The action called for each incoming ISubscriber to trigger emissions of signals.</param>
        /// <param name="strategy">The backpressure strategy to use when the emitter overproduces items.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<T> Create<T>(Action<IFlowableEmitter<T>> emitter, BackpressureStrategy strategy)
        {
            return new FlowableCreate<T>(emitter, strategy);
        }

        /// <summary>
        /// Generates items in a backpressure-aware manner without any per-ISubscriber state.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="emitter">The action called to signal an event for each downstream request.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<T> Generate<T>(Action<IGeneratorEmitter<T>> emitter)
        {
            return Generate<T, object>(() => null, (s, e) => { emitter(e); return null; }, s => { });
        }

        /// <summary>
        /// Generates items in a backpressure-aware manner with a per-ISubscriber state.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <typeparam name="S">The state type.</typeparam>
        /// <param name="emitter">The action called to signal an event for each downstream request.</param>
        /// <param name="stateFactory">The function called to create the initial state per ISubscriber.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<T> Generate<T, S>(Func<S> stateFactory, Action<S, IGeneratorEmitter<T>> emitter)
        {
            return Generate<T, S>(stateFactory, (s, e) => { emitter(s, e); return s; }, s => { });
        }

        /// <summary>
        /// Generates items in a backpressure-aware manner with a per-ISubscriber state
        /// and a state cleanup callback.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <typeparam name="S">The state type.</typeparam>
        /// <param name="emitter">The action called to signal an event for each downstream request.</param>
        /// <param name="stateFactory">The function called to create the initial state per ISubscriber.</param>
        /// <param name="stateCleanup">The action called to cleanup the state.</param>
        /// <param name="eager">If true, the stateCleanup is called before the terminal signal is emitted;
        /// if false, the stateCleanup is called after the terminal signal is emitted.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<T> Generate<T, S>(Func<S> stateFactory, Action<S, IGeneratorEmitter<T>> emitter, Action<S> stateCleanup, bool eager = false)
        {
            return Generate<T, S>(stateFactory, (s, e) => { emitter(s, e); return s; }, stateCleanup, eager);
        }

        /// <summary>
        /// Generates items in a backpressure-aware manner with a per-ISubscriber state
        /// that is updated each time the emitter funtion is invoked.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <typeparam name="S">The state type.</typeparam>
        /// <param name="emitter">The action called to signal an event for each downstream request.</param>
        /// <param name="stateFactory">The function called to create the initial state per ISubscriber.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<T> Generate<T, S>(Func<S> stateFactory, Func<S, IGeneratorEmitter<T>, S> emitter)
        {
            return Generate<T, S>(stateFactory, emitter, EmptyConsumer<S>.Instance);
        }

        /// <summary>
        /// Generates items in a backpressure-aware manner with a per-ISubscriber state
        /// that is updated each time the emitter funtion is invoked and a state cleanup callback.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <typeparam name="S">The state type.</typeparam>
        /// <param name="emitter">The action called to signal an event for each downstream request.</param>
        /// <param name="stateFactory">The function called to create the initial state per ISubscriber.</param>
        /// <param name="stateCleanup">The action called to cleanup the state.</param>
        /// <param name="eager">If true, the stateCleanup is called before the terminal signal is emitted;
        /// if false, the stateCleanup is called after the terminal signal is emitted.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<T> Generate<T, S>(Func<S> stateFactory, Func<S, IGeneratorEmitter<T>, S> emitter, Action<S> stateCleanup, bool eager = false)
        {
            return new FlowableGenerate<T, S>(stateFactory, emitter, stateCleanup, eager);
        }

        /// <summary>
        /// Emits the items of the given params array.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="items">The items to emit.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<T> FromArray<T>(params T[] items)
        {
            return new FlowableArray<T>(items);
        }

        /// <summary>
        /// Emits the items of the given IEnumerable.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="items">The items to emit.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<T> FromEnumerable<T>(IEnumerable<T> items)
        {
            return new FlowableEnumerable<T>(items);
        }

        /// <summary>
        /// Emits a range of ints from start (inclusive) to start + count (exclusive).
        /// </summary>
        /// <param name="start">The starting value.</param>
        /// <param name="count">The number of ints to emit.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<int> Range(int start, int count)
        {
            return new FlowableRange(start, start + count);
        }

        /// <summary>
        /// Defers the creation of the actual IPublisher, allowing a per-ISubscriber
        /// state and/or individual IPublishers.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="supplier">The function that returns an IPublisher for each ISubscriber.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<T> Defer<T>(Func<IPublisher<T>> supplier)
        {
            return new FlowableDefer<T>(supplier);
        }

        /// <summary>
        /// Emits 0L after the specified delay running on the Executors.Computation executor.
        /// </summary>
        /// <param name="delay">The delay timespan.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<long> Timer(TimeSpan delay)
        {
            return Timer(delay, Executors.Computation);
        }

        /// <summary>
        /// Emits 0L after the specified delay running on the given executor.
        /// </summary>
        /// <param name="delay">The delay timespan.</param>
        /// <param name="executor">The executor to use for delaying the emission.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<long> Timer(TimeSpan delay, IExecutorService executor)
        {
            return new FlowableTimer(delay, executor);
        }

        /// <summary>
        /// Emits an ever increasing long values periodically on the Executors.Computation
        /// executor.
        /// </summary>
        /// <param name="period">The initial and periodic delay.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<long> Interval(TimeSpan period)
        {
            return Interval(period, period, Executors.Computation);
        }

        /// <summary>
        /// Emits an ever increasing long values periodically on the Executors.Computation
        /// executor.
        /// </summary>
        /// <param name="initialDelay">The initial delay.</param>
        /// <param name="period">The periodic delay.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<long> Interval(TimeSpan initialDelay, TimeSpan period)
        {
            return Interval(initialDelay, period, Executors.Computation);
        }

        /// <summary>
        /// Emits an ever increasing long values periodically on the given
        /// executor.
        /// </summary>
        /// <param name="period">The initial and periodic delay.</param>
        /// <param name="executor">The executor to use for delaying the emission.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<long> Interval(TimeSpan period, IExecutorService executor)
        {
            return Interval(period, period, executor);
        }

        /// <summary>
        /// Emits an ever increasing long values periodically on the given
        /// executor.
        /// </summary>
        /// <param name="initialDelay">The initial delay.</param>
        /// <param name="period">The periodic delay.</param>
        /// <param name="executor">The executor to use for delaying the emission.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<long> Interval(TimeSpan initialDelay, TimeSpan period, IExecutorService executor)
        {
            return new FlowableInterval(initialDelay, period, executor);
        }

        /// <summary>
        /// Creates a per-ISubscriber resource, generates an IPublisher from that resource
        /// and relays its signals until completion when the resource is cleaned up via a callback.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <typeparam name="D">The resource type.</typeparam>
        /// <param name="resourceFactory">The function that creates a per-ISubscriber resource.</param>
        /// <param name="resourceMapper">The function that given the resource, it returns an IPublisher to relay events of.</param>
        /// <param name="resourceCleanup">The function that cleans up the resource once the IPublisher has terminated or the
        /// sequence is cancelled</param>
        /// <param name="eager">If true, the stateCleanup is called before the terminal signal is emitted;
        /// if false, the stateCleanup is called after the terminal signal is emitted.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<T> Using<T, D>(Func<D> resourceFactory, Func<D, IPublisher<T>> resourceMapper, Action<D> resourceCleanup = null, bool eager = true)
        {
            return new FlowableUsing<T, D>(resourceFactory, resourceMapper, resourceCleanup, eager);
        }

        // ********************************************************************************
        // Multi-source factory methods
        // ********************************************************************************

        /// <summary>
        /// Relays events of the IPublisher that signals first.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="sources">The params array of IPublisher instances.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<T> Amb<T>(params IPublisher<T>[] sources) {
            var n = sources.Length;
            if (n == 0)
            {
                return Empty<T>();
            }
            if (n == 1)
            {
                return FromPublisher(sources[0]);
            }
            return new FlowableAmbArray<T>(sources);
        }

        /// <summary>
        /// Relays events of the IPublisher that signals first.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="sources">The enumerable of IPublisher instances.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<T> Amb<T>(IEnumerable<IPublisher<T>> sources)
        {
            return new FlowableAmbEnumerable<T>(sources);
        }

        /// <summary>
        /// Concatenates elements of IPublishers one after the other.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="sources">The params array of IPublisher sources.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<T> Concat<T>(params IPublisher<T>[] sources)
        {
            return new FlowableConcatArray<T>(sources);
        }

        /// <summary>
        /// Concatenates elements of IPublishers one after the other.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="sources">The enumerable of IPublisher sources.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<T> Concat<T>(IEnumerable<IPublisher<T>> sources)
        {
            return new FlowableConcatEnumerable<T>(sources);
        }

        /// <summary>
        /// Concatenates elements of IPublishers one after the other.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="sources">The IPublisher of IPublisher sources.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<T> Concat<T>(this IPublisher<IPublisher<T>> sources)
        {
            return Concat(sources, BufferSize());
        }

        /// <summary>
        /// Concatenates elements of IPublishers one after the other.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="sources">The IPublisher of IPublisher sources.</param>
        /// <param name="prefetch">Number of inner IPublishers to prefetch from the outer IPublisher.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<T> Concat<T>(this IPublisher<IPublisher<T>> sources, int prefetch)
        {
            return new FlowableConcatMapPublisher<IPublisher<T>, T>(sources, Identity<IPublisher<T>>.Instance, prefetch);
        }

        /// <summary>
        /// Concatenates the IPublishers one after the other while pre-running them.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="sources">The params array of IPublisher sources.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<T> ConcatEager<T>(params IPublisher<T>[] sources)
        {
            return FromArray(sources).ConcatMapEager(Identity<IPublisher<T>>.Instance);
        }

        /// <summary>
        /// Concatenates the IPublishers one after the other while pre-running them.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="sources">The enumerable of IPublisher sources.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<T> ConcatEager<T>(IEnumerable<IPublisher<T>> sources)
        {
            return ConcatEager(sources, BufferSize(), BufferSize());
        }

        /// <summary>
        /// Concatenates the IPublishers one after the other while pre-running them.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="sources">The enumerable of IPublisher sources.</param>
        /// <param name="maxConcurrency">The maximum number of IPublishers to run at once.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<T> ConcatEager<T>(IEnumerable<IPublisher<T>> sources, int maxConcurrency)
        {
            return ConcatEager(sources, maxConcurrency, BufferSize());
        }

        /// <summary>
        /// Concatenates the IPublishers one after the other while pre-running them.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="sources">The enumerable of IPublisher sources.</param>
        /// <param name="maxConcurrency">The maximum number of IPublishers to run at once.</param>
        /// <param name="prefetch">Number of items to prefetch from the inner IPublishers.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<T> ConcatEager<T>(IEnumerable<IPublisher<T>> sources, int maxConcurrency, int prefetch)
        {
            return FromEnumerable(sources).ConcatMapEager(Identity<IPublisher<T>>.Instance, maxConcurrency, prefetch);
        }

        /// <summary>
        /// Concatenates elements of IPublishers one after the other.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="sources">The IPublisher of IPublisher sources.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<T> ConcatEager<T>(this IPublisher<IPublisher<T>> sources)
        {
            return ConcatEager(sources, BufferSize(), BufferSize());
        }

        /// <summary>
        /// Concatenates the IPublishers one after the other while pre-running them.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="sources">The enumerable of IPublisher sources.</param>
        /// <param name="maxConcurrency">The maximum number of IPublishers to run at once.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<T> ConcatEager<T>(this IPublisher<IPublisher<T>> sources, int maxConcurrency)
        {
            return ConcatEager(sources, maxConcurrency, BufferSize());
        }

        /// <summary>
        /// Concatenates the IPublishers one after the other while pre-running them.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="sources">The enumerable of IPublisher sources.</param>
        /// <param name="maxConcurrency">The maximum number of IPublishers to run at once.</param>
        /// <param name="prefetch">Number of items to prefetch from the inner IPublishers.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<T> ConcatEager<T>(this IPublisher<IPublisher<T>> sources, int maxConcurrency, int prefetch)
        {
            return new FlowableConcatMapEagerPublisher<IPublisher<T>, T>(sources, Identity<IPublisher<T>>.Instance, maxConcurrency, prefetch);
        }

        /// <summary>
        /// Merges an array of IPublishers.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="sources">The params array of IPublisher instances.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<T> Merge<T>(params IPublisher<T>[] sources)
        {
            return FromArray(sources).FlatMap(Identity<IPublisher<T>>.Instance);
        }

        /// <summary>
        /// Merges an array of IPublishers.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="sources">The enumerable of IPublisher instances.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<T> Merge<T>(IEnumerable<IPublisher<T>> sources)
        {
            return FromEnumerable(sources).FlatMap(Identity<IPublisher<T>>.Instance);
        }

        /// <summary>
        /// Merges an array of IPublishers up to a maximum at a time.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="sources">The params array of IPublisher source.</param>
        /// <param name="maxConcurrency">The maximum number of inner IPublishers to merge at once.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<T> Merge<T>(IEnumerable<IPublisher<T>> sources, int maxConcurrency)
        {
            return FromEnumerable(sources).FlatMap(Identity<IPublisher<T>>.Instance, maxConcurrency);
        }

        /// <summary>
        /// Merges an array of IPublishers up to a maximum at a time.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="sources">The params array of IPublisher source.</param>
        /// <param name="maxConcurrency">The maximum number of inner IPublishers to merge at once.</param>
        /// <param name="prefetch">The number of items to prefetch from the inner IPublishers.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<T> Merge<T>(IEnumerable<IPublisher<T>> sources, int maxConcurrency, int prefetch)
        {
            return FromEnumerable(sources).FlatMap(Identity<IPublisher<T>>.Instance, maxConcurrency, prefetch);
        }

        /// <summary>
        /// Merges an IPublisher sequence of IPublishers.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="sources">The IPublisher of inner IPublisher instances.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<T> Merge<T>(this IPublisher<IPublisher<T>> sources)
        {
            return Merge(sources, BufferSize(), BufferSize());
        }

        /// <summary>
        /// Merges an IPublisher sequence of IPublishers up to a maximum at a time.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="sources">The IPublisher of inner IPublisher instances.</param>
        /// <param name="maxConcurrency">The maximum number of inner IPublishers to merge at once.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<T> Merge<T>(this IPublisher<IPublisher<T>> sources, int maxConcurrency)
        {
            return Merge(sources, maxConcurrency, BufferSize());
        }

        /// <summary>
        /// Merges an IPublisher sequence of IPublishers up to a maximum at a time
        /// and using the specified bufferSize to prefetch items from the inner IPublishers.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="sources">The IPublisher of inner IPublisher instances.</param>
        /// <param name="maxConcurrency">The maximum number of inner IPublishers to merge at once.</param>
        /// <param name="bufferSize">The number of items to prefetch from the inner IPublishers.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<T> Merge<T>(this IPublisher<IPublisher<T>> sources, int maxConcurrency, int bufferSize)
        {
            return new FlowableFlatMapPublisher<IPublisher<T>, T>(sources, Identity<IPublisher<T>>.Instance, maxConcurrency, bufferSize);
        }

        /// <summary>
        /// Combines the latest elements from all the IPublishers via a combiner function.
        /// </summary>
        /// <typeparam name="T">The common input value type.</typeparam>
        /// <typeparam name="R">The output value type.</typeparam>
        /// <param name="combiner">The function that receives the array of the latest items and
        /// returns the value to be emitted to downstream.</param>
        /// <param name="sources">The params array of IPublisher sources.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<R> CombineLatest<T, R>(Func<T[], R> combiner, params IPublisher<T>[] sources)
        {
            return CombineLatest(combiner, BufferSize(), sources);
        }

        /// <summary>
        /// Combines the latest elements from all the IPublishers via a combiner function.
        /// </summary>
        /// <typeparam name="T">The common input value type.</typeparam>
        /// <typeparam name="R">The output value type.</typeparam>
        /// <param name="combiner">The function that receives the array of the latest items and
        /// returns the value to be emitted to downstream.</param>
        /// <param name="prefetch">The number of items to prefetch from each IPublisher.</param>
        /// <param name="sources">The params array of IPublisher sources.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<R> CombineLatest<T, R>(Func<T[], R> combiner, int prefetch, params IPublisher<T>[] sources)
        {
            var n = sources.Length;
            if (n == 0)
            {
                return Empty<R>();
            }
            if (n == 1)
            {
                return FromPublisher(sources[0]).Map(v => combiner(new T[] { v }));
            }
            return new FlowableCombineLatest<T, R>(sources, combiner, prefetch);
        }

        /// <summary>
        /// Combines the latest elements from all the IPublishers via a combiner function.
        /// </summary>
        /// <typeparam name="T">The common input value type.</typeparam>
        /// <typeparam name="R">The output value type.</typeparam>
        /// <param name="sources">The enumerable of IPublisher sources.</param>
        /// <param name="combiner">The function that receives the array of the latest items and
        /// returns the value to be emitted to downstream.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<R> CombineLatest<T, R>(IEnumerable<IPublisher<T>> sources, Func<T[], R> combiner)
        {
            return CombineLatest(sources, combiner, BufferSize());
        }

        /// <summary>
        /// Combines the latest elements from all the IPublishers via a combiner function.
        /// </summary>
        /// <typeparam name="T">The common input value type.</typeparam>
        /// <typeparam name="R">The output value type.</typeparam>
        /// <param name="sources">The enumerable of IPublisher sources.</param>
        /// <param name="combiner">The function that receives the array of the latest items and
        /// returns the value to be emitted to downstream.</param>
        /// <param name="prefetch">The number of items to prefetch from each IPublisher.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<R> CombineLatest<T, R>(IEnumerable<IPublisher<T>> sources, Func<T[], R> combiner, int prefetch)
        {
            return new FlowableCombineLatestEnumerable<T, R>(sources, combiner, prefetch);
        }

        /// <summary>
        /// Converts a sequence of type T into a sequence of (boxed) objects.
        /// </summary>
        /// <typeparam name="T">The input value type</typeparam>
        /// <param name="source">The source IFlowable instance.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<object> Boxed<T>(this IPublisher<T> source)
        {
            return new FlowableBoxed<T>(source);
        }

        /// <summary>
        /// Combines the latest elements from all the IPublishers via a combiner function.
        /// </summary>
        /// <typeparam name="T1">The first input value type.</typeparam>
        /// <typeparam name="T2">The first input value type.</typeparam>
        /// <typeparam name="R">The output value type.</typeparam>
        /// <param name="source1">The first input IPublisher source.</param>
        /// <param name="source2">The second input IPublisher source.</param>
        /// <param name="combiner">The function that receives the array of the latest items and
        /// returns the value to be emitted to downstream.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<R> CombineLatest<T1, T2, R>(IPublisher<T1> source1, IPublisher<T2> source2, Func<T1, T2, R> combiner)
        {
            return CombineLatest<object, R>(
                a => combiner((T1)a[0], (T2)a[1]), 
                source1.Boxed(), source2.Boxed());
        }

        /// <summary>
        /// Combines the latest elements from all the IPublishers via a combiner function.
        /// </summary>
        /// <typeparam name="T1">The first input value type.</typeparam>
        /// <typeparam name="T2">The first input value type.</typeparam>
        /// <typeparam name="T3">The third input value type.</typeparam>
        /// <typeparam name="R">The output value type.</typeparam>
        /// <param name="source1">The first input IPublisher source.</param>
        /// <param name="source2">The second input IPublisher source.</param>
        /// <param name="source3">The third input IPublisher source.</param>
        /// <param name="combiner">The function that receives the array of the latest items and
        /// returns the value to be emitted to downstream.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<R> CombineLatest<T1, T2, T3, R>(IPublisher<T1> source1, IPublisher<T2> source2,
            IPublisher<T3> source3, Func<T1, T2, T3, R> combiner)
        {
            return CombineLatest<object, R>(
                a => combiner((T1)a[0], (T2)a[1], (T3)a[2]),
                source1.Boxed(), source2.Boxed(), source3.Boxed());
        }

        /// <summary>
        /// Combines the latest elements from all the IPublishers via a combiner function.
        /// </summary>
        /// <typeparam name="T1">The first input value type.</typeparam>
        /// <typeparam name="T2">The first input value type.</typeparam>
        /// <typeparam name="T3">The third input value type.</typeparam>
        /// <typeparam name="T4">The fourth input value type.</typeparam>
        /// <typeparam name="R">The output value type.</typeparam>
        /// <param name="source1">The first input IPublisher source.</param>
        /// <param name="source2">The second input IPublisher source.</param>
        /// <param name="source3">The third input IPublisher source.</param>
        /// <param name="source4">The fourth input IPublisher source.</param>
        /// <param name="combiner">The function that receives the array of the latest items and
        /// returns the value to be emitted to downstream.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<R> CombineLatest<T1, T2, T3, T4, R>(IPublisher<T1> source1, IPublisher<T2> source2,
            IPublisher<T3> source3, IPublisher<T4> source4, Func<T1, T2, T3, T4, R> combiner)
        {
            return CombineLatest<object, R>(
                a => combiner((T1)a[0], (T2)a[1], (T3)a[2], (T4)a[3]),
                source1.Boxed(), source2.Boxed(), source3.Boxed(), source4.Boxed());
        }

        /// <summary>
        /// Combines the next elements from all the IPublishers via a zipper function.
        /// </summary>
        /// <typeparam name="T">The common input value type.</typeparam>
        /// <typeparam name="R">The output value type.</typeparam>
        /// <param name="zipper">The function that receives the array of the next items and
        /// returns the value to be emitted to downstream.</param>
        /// <param name="sources">The params array of IPublisher sources.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<R> Zip<T, R>(Func<T[], R> zipper, params IPublisher<T>[] sources)
        {
            return Zip(zipper, BufferSize(), sources);
        }

        /// <summary>
        /// Combines the next elements from all the IPublishers via a zipper function.
        /// </summary>
        /// <typeparam name="T">The common input value type.</typeparam>
        /// <typeparam name="R">The output value type.</typeparam>
        /// <param name="zipper">The function that receives the array of the next items and
        /// returns the value to be emitted to downstream.</param>
        /// <param name="prefetch">The number of items to prefetch from each IPublisher.</param>
        /// <param name="sources">The params array of IPublisher sources.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<R> Zip<T, R>(Func<T[], R> zipper, int prefetch, params IPublisher<T>[] sources)
        {
            return new FlowableZipArray<T, R>(sources, zipper, prefetch);
        }

        /// <summary>
        /// Combines the next elements from all the IPublishers via a zipper function.
        /// </summary>
        /// <typeparam name="T">The common input value type.</typeparam>
        /// <typeparam name="R">The output value type.</typeparam>
        /// <param name="sources">The enumerable of IPublisher sources.</param>
        /// <param name="zipper">The function that receives the array of the next items and
        /// returns the value to be emitted to downstream.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<R> Zip<T, R>(IEnumerable<IPublisher<T>> sources, Func<T[], R> zipper)
        {
            return Zip(sources, zipper, BufferSize());
        }

        /// <summary>
        /// Combines the next elements from all the IPublishers via a zipper function.
        /// </summary>
        /// <typeparam name="T">The common input value type.</typeparam>
        /// <typeparam name="R">The output value type.</typeparam>
        /// <param name="sources">The enumerable of IPublisher sources.</param>
        /// <param name="zipper">The function that receives the array of the next items and
        /// returns the value to be emitted to downstream.</param>
        /// <param name="prefetch">The number of items to prefetch from each IPublisher.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<R> Zip<T, R>(IEnumerable<IPublisher<T>> sources, Func<T[], R> zipper, int prefetch)
        {
            return new FlowableZipEnumerable<T, R>(sources, zipper, prefetch);
        }

        /// <summary>
        /// Combines the next elements from all the IPublishers via a zipper function.
        /// </summary>
        /// <typeparam name="T1">The first input value type.</typeparam>
        /// <typeparam name="T2">The first input value type.</typeparam>
        /// <typeparam name="R">The output value type.</typeparam>
        /// <param name="source1">The first input IPublisher source.</param>
        /// <param name="source2">The second input IPublisher source.</param>
        /// <param name="zipper">The function that receives the array of the next items and
        /// returns the value to be emitted to downstream.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<R> Zip<T1, T2, R>(IPublisher<T1> source1, IPublisher<T2> source2, Func<T1, T2, R> zipper)
        {
            return Zip<object, R>(
                a => zipper((T1)a[0], (T2)a[1]),
                source1.Boxed(), source2.Boxed());
        }

        /// <summary>
        /// Combines the next elements from all the IPublishers via a zipper function.
        /// </summary>
        /// <typeparam name="T1">The first input value type.</typeparam>
        /// <typeparam name="T2">The first input value type.</typeparam>
        /// <typeparam name="T3">The third input value type.</typeparam>
        /// <typeparam name="R">The output value type.</typeparam>
        /// <param name="source1">The first input IPublisher source.</param>
        /// <param name="source2">The second input IPublisher source.</param>
        /// <param name="source3">The third input IPublisher source.</param>
        /// <param name="zipper">The function that receives the array of the next items and
        /// returns the value to be emitted to downstream.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<R> Zip<T1, T2, T3, R>(IPublisher<T1> source1, IPublisher<T2> source2, 
            IPublisher<T3> source3, Func<T1, T2, T3, R> zipper)
        {
            return Zip<object, R>(
                a => zipper((T1)a[0], (T2)a[1], (T3)a[2]),
                source1.Boxed(), source2.Boxed(), source3.Boxed());
        }

        /// <summary>
        /// Combines the next elements from all the IPublishers via a zipper function.
        /// </summary>
        /// <typeparam name="T1">The first input value type.</typeparam>
        /// <typeparam name="T2">The first input value type.</typeparam>
        /// <typeparam name="T3">The third input value type.</typeparam>
        /// <typeparam name="T4">The fourth input value type.</typeparam>
        /// <typeparam name="R">The output value type.</typeparam>
        /// <param name="source1">The first input IPublisher source.</param>
        /// <param name="source2">The second input IPublisher source.</param>
        /// <param name="source3">The third input IPublisher source.</param>
        /// <param name="source4">The fourth input IPublisher source.</param>
        /// <param name="zipper">The function that receives the array of the next items and
        /// returns the value to be emitted to downstream.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<R> Zip<T1, T2, T3, T4, R>(IPublisher<T1> source1, IPublisher<T2> source2,
            IPublisher<T3> source3, IPublisher<T4> source4, Func<T1, T2, T3, T4, R> zipper)
        {
            return Zip<object, R>(
                a => zipper((T1)a[0], (T2)a[1], (T3)a[2], (T4)a[3]),
                source1.Boxed(), source2.Boxed(), source3.Boxed(), source4.Boxed());
        }

        /// <summary>
        /// Switches to emitting the items of the inner IPublisher when the outer IPublisher
        /// produces that IPublisher.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="sources">The IPublisher producing the inner IPublisher instances</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<T> SwitchOnNext<T>(IPublisher<IPublisher<T>> sources)
        {
            return SwitchOnNext(sources, BufferSize());
        }

        /// <summary>
        /// Switches to emitting the items of the inner IPublisher when the outer IPublisher
        /// produces that IPublisher.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="sources">The IPublisher producing the inner IPublisher instances</param>
        /// <param name="prefetch">The number of items to prefetch from each iner IPublisher source.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<T> SwitchOnNext<T>(IPublisher<IPublisher<T>> sources, int prefetch)
        {
            return new FlowableSwitchMapPublisher<IPublisher<T>, T>(sources, Identity<IPublisher<T>>.Instance, prefetch);
        }

        // ********************************************************************************
        // Instance operators
        // ********************************************************************************

        /// <summary>
        /// Maps the upstream values into values to be emitted to downstream via
        /// a mapper function.
        /// </summary>
        /// <typeparam name="T">The upstream value type.</typeparam>
        /// <typeparam name="R">The downstream value type.</typeparam>
        /// <param name="source">The source IFlowable instance.</param>
        /// <param name="mapper">The function that maps each upstream value
        /// into a downstream value.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<R> Map<T, R>(this IFlowable<T> source, Func<T, R> mapper)
        {
            return new FlowableMap<T, R>(source, mapper);
        }
        
        /// <summary>
        /// Maps each upstream value into an IPublisher, runs them one at a time and after the other
        /// and takes their first item as the result to be emitted towards the downstream.
        /// </summary>
        /// <typeparam name="T">The input value type.</typeparam>
        /// <typeparam name="R">The result value type.</typeparam>
        /// <param name="source">The source IFlowable instance.</param>
        /// <param name="mapper">The function that maps an upstream value into an IPublisher,
        /// runs it and takes its first value as the result to be emitted. An empty IPublisher
        /// will not trigger any emission.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<R> MapAsync<T, R>(this IFlowable<T> source, Func<T, IPublisher<R>> mapper)
        {
            return MapAsync(source, mapper, Func2Second<T, R>.Instance, BufferSize());
        }

        /// <summary>
        /// Maps each upstream value into an IPublisher, runs them one at a time and after the other
        /// and takes their first item as the result to be emitted towards the downstream.
        /// </summary>
        /// <typeparam name="T">The input value type.</typeparam>
        /// <typeparam name="R">The result value type.</typeparam>
        /// <param name="source">The source IFlowable instance.</param>
        /// <param name="mapper">The function that maps an upstream value into an IPublisher,
        /// runs it and takes its first value as the result to be emitted. An empty IPublisher
        /// will not trigger any emission.</param>
        /// <param name="bufferSize">The size of the internal buffer and the prefetch amount of upstream
        /// values; upstream items are held in a buffer until each of them gets a response from the mapped IPublisher.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<R> MapAsync<T, R>(this IFlowable<T> source, Func<T, IPublisher<R>> mapper, int bufferSize)
        {
            return MapAsync(source, mapper, Func2Second<T, R>.Instance, bufferSize);
        }

        /// <summary>
        /// Maps each upstream value into an IPublisher, runs them one at a time and after the other
        /// and takes their first item as the input to a result-mapper function that takes both the
        /// original upstream item and the response from the IPublisher to create the result emitted
        /// towards the downstream.
        /// </summary>
        /// <typeparam name="T">The input value type.</typeparam>
        /// <typeparam name="U">The intermediate type of the async mapped result</typeparam>
        /// <typeparam name="R">The result value type.</typeparam>
        /// <param name="source">The source IFlowable instance.</param>
        /// <param name="mapper">The function that maps an upstream value into an IPublisher,
        /// runs it and takes its first value as the result to be emitted. An empty IPublisher
        /// will not trigger any emission.</param>
        /// <param name="resultMapper">The function that takes the original upstream item and the
        /// single value returned by the associated IPublisher and returns the value to be emitted.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<R> MapAsync<T, U, R>(this IFlowable<T> source, Func<T, IPublisher<U>> mapper, Func<T, U, R> resultMapper)
        {
            return new FlowableMapAsync<T, U, R>(source, mapper, resultMapper, BufferSize());
        }

        /// <summary>
        /// Maps each upstream value into an IPublisher, runs them one at a time and after the other
        /// and takes their first item as the input to a result-mapper function that takes both the
        /// original upstream item and the response from the IPublisher to create the result emitted
        /// towards the downstream.
        /// </summary>
        /// <typeparam name="T">The input value type.</typeparam>
        /// <typeparam name="U">The intermediate type of the async mapped result</typeparam>
        /// <typeparam name="R">The result value type.</typeparam>
        /// <param name="source">The source IFlowable instance.</param>
        /// <param name="mapper">The function that maps an upstream value into an IPublisher,
        /// runs it and takes its first value as the result to be emitted. An empty IPublisher
        /// will not trigger any emission.</param>
        /// <param name="resultMapper">The function that takes the original upstream item and the
        /// single value returned by the associated IPublisher and returns the value to be emitted.</param>
        /// <param name="bufferSize">The size of the internal buffer and the prefetch amount of upstream
        /// values; upstream items are held in a buffer until each of them gets a response from the mapped IPublisher.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<R> MapAsync<T, U, R>(this IFlowable<T> source, Func<T, IPublisher<U>> mapper, Func<T, U, R> resultMapper, int bufferSize)
        {
            return new FlowableMapAsync<T, U, R>(source, mapper, resultMapper, bufferSize);
        }

        /// <summary>
        /// Filters the upstream values based on a predicate function and
        /// allows those passing through for which the predicate returns true.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The source IFlowable instance.</param>
        /// <param name="predicate">The function that receives each item and
        /// should return true for those that should be passed onto the 
        /// downstream.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<T> Filter<T>(this IFlowable<T> source, Func<T, bool> predicate)
        {
            return new FlowableFilter<T>(source, predicate);
        }

        /// <summary>
        /// Maps each upstream value into an IPublisher, runs them one-by-one and after each other
        /// and in case they emit a true, the original upstream value is passed to downstream.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The source IFlowable instance.</param>
        /// <param name="predicate">The function that receives the upstream item and should
        /// return an IPublisher whose first boolean value determines if the upstream value
        /// is passed to the downstream. Empty IPublisher is considered false.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<T> FilterAsync<T>(this IFlowable<T> source, Func<T, IPublisher<bool>> predicate)
        {
            return FilterAsync(source, predicate, BufferSize());
        }

        /// <summary>
        /// Maps each upstream value into an IPublisher, runs them one-by-one and after each other
        /// and in case they emit a true, the original upstream value is passed to downstream.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The source IFlowable instance.</param>
        /// <param name="predicate">The function that receives the upstream item and should
        /// return an IPublisher whose first boolean value determines if the upstream value
        /// is passed to the downstream. Empty IPublisher is considered false.</param>
        /// <param name="bufferSize">The number of items to prefetch and store from upstream.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<T> FilterAsync<T>(this IFlowable<T> source, Func<T, IPublisher<bool>> predicate, int bufferSize)
        {
            return new FlowableFilterAsync<T>(source, predicate, bufferSize);
        }

        /// <summary>
        /// Consumes and relays up to the specified number of items from upstream,
        /// cancels it and emits a completion signal to the downstream.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The source IFlowable instance.</param>
        /// <param name="n">The number of items to let pass.</param>
        /// <param name="limitRequest">If true, the operator doesn't request
        /// more than <paramref name="n"/>; if false and if the downstream
        /// requests more than <paramref name="n"/>, the operator requests
        /// in an unbounded manner.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<T> Take<T>(this IFlowable<T> source, long n, bool limitRequest = false)
        {
            return new FlowableTake<T>(source, n, limitRequest);
        }

        /// <summary>
        /// Skips the given number of items from the source IFlowable and
        /// relays the rest.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The source IFlowable instance.</param>
        /// <param name="n">The number of items to skip.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<T> Skip<T>(this IFlowable<T> source, long n)
        {
            return new FlowableSkip<T>(source, n);
        }

        /// <summary>
        /// Takes the last number of items from the source IFlowable and relays them 
        /// to downstream.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The source IFlowable instance.</param>
        /// <param name="n">The number of last items to keep and relay.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<T> TakeLast<T>(this IFlowable<T> source, int n)
        {
            if (n <= 0)
            {
                return Empty<T>();
            }
            if (n == 1)
            {
                return new FlowableTakeLastOne<T>(source);
            }
            return new FlowableTakeLast<T>(source, n);
        }

        /// <summary>
        /// Skips the last given number of items from the source IFlowable.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The source IFlowable instance.</param>
        /// <param name="n">The number of items to skip from the end of the stream.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<T> SkipLast<T>(this IFlowable<T> source, int n)
        {
            if (n <= 0)
            {
                return source;
            }
            return new FlowableSkipLast<T>(source, n);
        }

        /// <summary>
        /// Collects the items of the upstream into a collection provided via 
        /// a function for each subscriber and via collector function.
        /// </summary>
        /// <typeparam name="T">The upstream value type.</typeparam>
        /// <typeparam name="C">The collection type.</typeparam>
        /// <param name="source">The source IFlowable instance.</param>
        /// <param name="collectionSupplier">The function called for each subscriber to generate
        /// a per-subscriber collection which will be emitted at the end.</param>
        /// <param name="collector">The function that "adds" the upstream item into the collection.</param>
        /// <returns></returns>
        public static IFlowable<C> Collect<T, C>(this IFlowable<T> source, Func<C> collectionSupplier, Action<C, T> collector)
        {
            return new FlowableCollect<T, C>(source, collectionSupplier, collector);
        }

        /// <summary>
        /// Reduces the elements of the source via a reducer function into a single
        /// value (or an empty sequence if the source is empty).
        /// </summary>
        /// <typeparam name="T">The input and output value type.</typeparam>
        /// <param name="source">The source IFlowable instance.</param>
        /// <param name="reducer">The function that takes the first and second item
        /// to produce the first output, then takes the third item and the last result
        /// to produce the second output, and so on.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<T> Reduce<T>(this IFlowable<T> source, Func<T, T, T> reducer)
        {
            return new FlowableReducePlain<T>(source, reducer);
        }

        /// <summary>
        /// Reduces the values of the source sequence via the help of an accumulator value
        /// and reducer function, starting with a default accumulator value, into
        /// a single final accumulated value.
        /// </summary>
        /// <typeparam name="T">The input value type.</typeparam>
        /// <typeparam name="R">The accumulator and output type.</typeparam>
        /// <param name="source">The source IFlowable sequence.</param>
        /// <param name="initialSupplier">The function that provides the initial accumulator value.</param>
        /// <param name="reducer">The function that takes the previous (or first) accumulator value and
        /// the current item to produce the next accumulator item.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<R> Reduce<T, R>(this IFlowable<T> source, Func<R> initialSupplier, Func<R, T, R> reducer)
        {
            return new FlowableReduce<T, R>(source, initialSupplier, reducer);
        }

        /// <summary>
        /// Collects all upstream values into a List and emits it to the downstream.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The source IFlowable instance.</param>
        /// <param name="capacityHint">The number of items expected from downstream, positive.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<IList<T>> ToList<T>(this IFlowable<T> source, int capacityHint = 10)
        {
            return Collect(source, () => new List<T>(capacityHint), ListAdd<T>.Instance);
        }

        /// <summary>
        /// Sums up a source of ints into a single int value.
        /// </summary>
        /// <param name="source">The source IFlowable instance.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<int> SumInt(this IFlowable<int> source)
        {
            return Reduce(source, IntAdd.Instance);
        }

        /// <summary>
        /// Sums up a source of longs into a single long value.
        /// </summary>
        /// <param name="source">The source IFlowable instance.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<long> SumLong(this IFlowable<long> source)
        {
            return Reduce(source, LongAdd.Instance);
        }

        /// <summary>
        /// Signals the largest integer value of the source.
        /// </summary>
        /// <param name="source">The source IFlowable of integers.</param>
        /// <returns>The new IFlowable instance</returns>
        public static IFlowable<int> MaxInt(this IFlowable<int> source)
        {
            return Reduce(source, IntMax.Instance);
        }

        /// <summary>
        /// Emits the maximum item from the source IFlowable based on a custom comparer.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The source IFlowable instance.</param>
        /// <param name="comparer">The comparer receiving the previous and the current item.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<T> Max<T>(this IFlowable<T> source, IComparer<T> comparer)
        {
            return Reduce(source, (a, b) => comparer.Compare(a, b) < 0 ? b : a);
        }

        /// <summary>
        /// Signals the largest long value of the source.
        /// </summary>
        /// <param name="source">The source IFlowable of long.</param>
        /// <returns>The new IFlowable instance</returns>
        public static IFlowable<long> MaxLong(this IFlowable<long> source)
        {
            return Reduce(source, LongMax.Instance);
        }

        /// <summary>
        /// Signals the smallest integer value of the source.
        /// </summary>
        /// <param name="source">The source IFlowable of integers.</param>
        /// <returns>The new IFlowable instance</returns>
        public static IFlowable<int> MinInt(this IFlowable<int> source)
        {
            return Reduce(source, IntMax.Instance);
        }

        /// <summary>
        /// Signals the smallest long value of the source.
        /// </summary>
        /// <param name="source">The source IFlowable of longs.</param>
        /// <returns>The new IFlowable instance</returns>
        public static IFlowable<long> MinLong(this IFlowable<long> source)
        {
            return Reduce(source, LongMax.Instance);
        }

        /// <summary>
        /// Emits the minimum item from the source IFlowable based on a custom comparer.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The source IFlowable instance.</param>
        /// <param name="comparer">The comparer receiving the previous and the current item.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<T> Min<T>(this IFlowable<T> source, IComparer<T> comparer)
        {
            return Reduce(source, (a, b) => comparer.Compare(a, b) < 0 ? a : b);
        }

        /// <summary>
        /// Ignores all upstream values and only relay the terminal event.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The source IFlowable instance.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<T> IgnoreElements<T>(this IFlowable<T> source)
        {
            return new FlowableIgnoreElements<T>(source);
        }

        /// <summary>
        /// Maps each upstream value into an IPublisher and merges them
        /// together in a potentially interleaved fashion.
        /// </summary>
        /// <typeparam name="T">The upstream value type.</typeparam>
        /// <typeparam name="R">The result value type.</typeparam>
        /// <param name="source">The source IFlowable instance.</param>
        /// <param name="mapper">The function that takes each upstream item
        /// and turns them into an IPublisher source to be merged.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<R> FlatMap<T, R>(this IFlowable<T> source, Func<T, IPublisher<R>> mapper)
        {
            return FlatMap(source, mapper, BufferSize(), BufferSize());
        }

        /// <summary>
        /// Maps each upstream value into an IPublisher and merges them
        /// together in a potentially interleaved fashion, running
        /// up to the specified number of IPublisher's at a time.
        /// </summary>
        /// <typeparam name="T">The upstream value type.</typeparam>
        /// <typeparam name="R">The result value type.</typeparam>
        /// <param name="source">The source IFlowable instance.</param>
        /// <param name="mapper">The function that takes each upstream item
        /// and turns them into an IPublisher source to be merged.</param>
        /// <param name="maxConcurrency">The maximum number of IPublishers to run at a time.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<R> FlatMap<T, R>(this IFlowable<T> source, Func<T, IPublisher<R>> mapper, int maxConcurrency)
        {
            return FlatMap(source, mapper, maxConcurrency, BufferSize());
        }

        /// <summary>
        /// Maps each upstream value into an IPublisher and merges them
        /// together in a potentially interleaved fashion, running
        /// up to the specified number of IPublisher's at a time
        /// and prefetches the given number of items from each IPublishers.
        /// </summary>
        /// <typeparam name="T">The upstream value type.</typeparam>
        /// <typeparam name="R">The result value type.</typeparam>
        /// <param name="source">The source IFlowable instance.</param>
        /// <param name="mapper">The function that takes each upstream item
        /// and turns them into an IPublisher source to be merged.</param>
        /// <param name="maxConcurrency">The maximum number of IPublishers to run at a time.</param>
        /// <param name="bufferSize">The number of items to prefetch and buffer from each inner IPublisher.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<R> FlatMap<T, R>(this IFlowable<T> source, Func<T, IPublisher<R>> mapper, int maxConcurrency, int bufferSize)
        {
            return new FlowableFlatMap<T, R>(source, mapper, maxConcurrency, bufferSize);
        }

        /// <summary>
        /// Subscribes to and optionally requests on the specified executor's thread.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The source IFlowable instance.</param>
        /// <param name="executor">The IExecutorService whose IExecutorWorker to use for
        /// subscribing and requesting.</param>
        /// <param name="requestOn">If true, requests towards the upstream will be executed on
        /// the same worker thread; if false, requests are forwarded on the same thread it
        /// was issued.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<T> SubscribeOn<T>(this IFlowable<T> source, IExecutorService executor, bool requestOn = true)
        {
            return new FlowableSubscribeOn<T>(source, executor, requestOn);
        }

        /// <summary>
        /// Makes sure signals from upstream are delivered to the downstream from
        /// a thread specified by the given IExecutorService's worker.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The source IFlowable instance.</param>
        /// <param name="executor">The IExecutorService whose worker to use for delivering
        /// signals to downstream.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<T> ObserveOn<T>(this IFlowable<T> source, IExecutorService executor)
        {
            return ObserveOn(source, executor, BufferSize());
        }

        /// <summary>
        /// Makes sure signals from upstream are delivered to the downstream from
        /// a thread specified by the given IExecutorService's worker and
        /// using the given buffer size for prefetching and buffering items
        /// until the downstream can process them.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The source IFlowable instance.</param>
        /// <param name="executor">The IExecutorService whose worker to use for delivering
        /// signals to downstream.</param>
        /// <param name="bufferSize">The number of items to prefetch and hold in the internal buffer.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<T> ObserveOn<T>(this IFlowable<T> source, IExecutorService executor, int bufferSize)
        {
            return new FlowableObserveOn<T>(source, executor, bufferSize);
        }

        /// <summary>
        /// Changes the request patter to request the given amount upfront and
        /// after 75% has been delivered, it request that 75% amount from upstream
        /// again.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The source IFlowable instance.</param>
        /// <param name="batchSize">The number of items to prefetch and hold
        /// in the buffer.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<T> RebatchRequests<T>(this IFlowable<T> source, int batchSize)
        {
            return ObserveOn(source, Executors.Immediate, batchSize);
        }

        /// <summary>
        /// Delays the emission of events from the source by the given time amount
        /// (shifts them in time, but the relative time-distance remains the same).
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The source IFlowable instance.</param>
        /// <param name="delay">The emission delay timespan.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<T> Delay<T>(this IFlowable<T> source, TimeSpan delay)
        {
            return Delay(source, delay, Executors.Computation);
        }

        /// <summary>
        /// Delays the emission of events from the source by the given time amount
        /// (shifts them in time, but the relative time-distance remains the same).
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The source IFlowable instance.</param>
        /// <param name="delay">The emission delay timespan.</param>
        /// <param name="executor">The custom executor where the delayed emission happens.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<T> Delay<T>(this IFlowable<T> source, TimeSpan delay, IExecutorService executor)
        {
            return new FlowableDelay<T>(source, delay, executor);
        }

        /// <summary>
        /// Delays the subscription to the source IFlowable until the other IFlowable
        /// signals an item or completes.
        /// </summary>
        /// <typeparam name="T">The main source's value type.</typeparam>
        /// <typeparam name="U">The other IPublisher's value type.</typeparam>
        /// <param name="source">The source IFlowable instance.</param>
        /// <param name="other">The other IFlowable instance.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<T> DelaySubscription<T, U>(this IFlowable<T> source, IPublisher<U> other)
        {
            return new FlowableDelaySubscription<T, U>(source, other);
        }

        /// <summary>
        /// Delays the subscription to the source IFlowable until the specified
        /// timespan elapses.
        /// </summary>
        /// <typeparam name="T">The source value type.</typeparam>
        /// <param name="source">The source IFlowable instance.</param>
        /// <param name="delay">The time delay before the actual subscription to source.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<T> DelaySubscription<T>(this IFlowable<T> source, TimeSpan delay)
        {
            return DelaySubscription(source, Timer(delay, Executors.Computation));
        }

        /// <summary>
        /// Delays the subscription to the source IFlowable until the specified
        /// timespan elapses.
        /// </summary>
        /// <typeparam name="T">The source value type.</typeparam>
        /// <param name="source">The source IFlowable instance.</param>
        /// <param name="delay">The time delay before the actual subscription to source.</param>
        /// <param name="executor">The IExecutorService where to wait for the delay.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<T> DelaySubscription<T>(this IFlowable<T> source, TimeSpan delay, IExecutorService executor)
        {
            return DelaySubscription(source, Timer(delay, executor));
        }

        /// <summary>
        /// Transforms items of the source into IPublishers and concatenates their items,
        /// in order and non-overlapping manner into a single sequence.
        /// </summary>
        /// <typeparam name="T">The upstream value type.</typeparam>
        /// <typeparam name="R">The result value type.</typeparam>
        /// <param name="source">The source IFlowable instance.</param>
        /// <param name="mapper">The function that receives the upstream value and
        /// returns an IPublisher instance for it.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<R> ConcatMap<T, R>(this IFlowable<T> source, Func<T, IPublisher<R>> mapper)
        {
            return ConcatMap(source, mapper, 2);
        }

        /// <summary>
        /// Transforms items of the source into IPublishers and concatenates their items,
        /// in order and non-overlapping manner into a single sequence.
        /// </summary>
        /// <typeparam name="T">The upstream value type.</typeparam>
        /// <typeparam name="R">The result value type.</typeparam>
        /// <param name="source">The source IFlowable instance.</param>
        /// <param name="mapper">The function that receives the upstream value and
        /// returns an IPublisher instance for it.</param>
        /// <param name="prefetch">The number of items to prefetch from the upstream.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<R> ConcatMap<T, R>(this IFlowable<T> source, Func<T, IPublisher<R>> mapper, int prefetch)
        {
            return new FlowableConcatMap<T, R>(source, mapper, prefetch);
        }

        /// <summary>
        /// Maps the upstream values into IPublishers, pre-runs them and
        /// concatenates their items in order.
        /// </summary>
        /// <typeparam name="T">The upstream value type.</typeparam>
        /// <typeparam name="R">The inner IPublisher and result value type.</typeparam>
        /// <param name="source">The source IFlowable instance</param>
        /// <param name="mapper">The mapper that turns an upstream value into an IPublisher.</param>
        /// <returns>The new IFlowable instance</returns>
        public static IFlowable<R> ConcatMapEager<T, R>(this IFlowable<T> source, Func<T, IPublisher<R>> mapper)
        {
            return ConcatMapEager(source, mapper, BufferSize(), BufferSize());
        }

        /// <summary>
        /// Maps the upstream values into IPublishers, pre-runs them and
        /// concatenates their items in order.
        /// </summary>
        /// <typeparam name="T">The upstream value type.</typeparam>
        /// <typeparam name="R">The inner IPublisher and result value type.</typeparam>
        /// <param name="source">The source IFlowable instance</param>
        /// <param name="mapper">The mapper that turns an upstream value into an IPublisher.</param>
        /// <param name="maxConcurrency">The maximum number of concurrently running inner IPublishers, positive</param>
        /// <returns>The new IFlowable instance</returns>
        public static IFlowable<R> ConcatMapEager<T, R>(this IFlowable<T> source, Func<T, IPublisher<R>> mapper, int maxConcurrency)
        {
            return ConcatMapEager(source, mapper, maxConcurrency, BufferSize());
        }

        /// <summary>
        /// Maps the upstream values into IPublishers, pre-runs them and
        /// concatenates their items in order.
        /// </summary>
        /// <typeparam name="T">The upstream value type.</typeparam>
        /// <typeparam name="R">The inner IPublisher and result value type.</typeparam>
        /// <param name="source">The source IFlowable instance</param>
        /// <param name="mapper">The mapper that turns an upstream value into an IPublisher.</param>
        /// <param name="maxConcurrency">The maximum number of concurrently running inner IPublishers, positive</param>
        /// <param name="prefetch">The number of items to prefetch from each inner IPublisher</param>
        /// <returns>The new IFlowable instance</returns>
        public static IFlowable<R> ConcatMapEager<T, R>(this IFlowable<T> source, Func<T, IPublisher<R>> mapper, int maxConcurrency, int prefetch)
        {
            return new FlowableConcatMapEager<T, R>(source, mapper, maxConcurrency, prefetch);
        }

        /// <summary>
        /// Hides the identity of the source IFlowable and prevents
        /// identity-based optimizations over the flow.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The source IFlowable instance</param>
        /// <returns>The new IFlowable instance</returns>
        public static IFlowable<T> Hide<T>(this IFlowable<T> source)
        {
            return new FlowableHide<T>(source);
        }

        /// <summary>
        /// Relays items that haven't been seen before (based on the default
        /// IEqualityComparer for the type T).
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The source Flowable instance</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<T> Distinct<T>(this IFlowable<T> source)
        {
            return Distinct(source, EqualityComparer<T>.Default);
        }

        /// <summary>
        /// Relays items that haven't been seen before (based on the 
        /// provided IEqualityComparer instance).
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The source Flowable instance</param>
        /// <param name="comparer">The comparer to compare items.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<T> Distinct<T>(this IFlowable<T> source, IEqualityComparer<T> comparer)
        {
            return new FlowableDistinct<T>(source, comparer);
        }

        /// <summary>
        /// Relays items if one item is different from the previous one
        /// (by comparing with the default IEqualityComparer for type T).
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The source IFlowable instance.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<T> DistinctUntilChanged<T>(this IFlowable<T> source)
        {
            return DistinctUntilChanged(source, EqualityComparer<T>.Default);
        }

        /// <summary>
        /// Relays items if one item is different from the previous one
        /// (by comparing with the given IEqualityComparer).
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The source IFlowable instance.</param>
        /// <param name="comparer">The comparer to use for comparing subsequent items.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<T> DistinctUntilChanged<T>(this IFlowable<T> source, IEqualityComparer<T> comparer)
        {
            return new FlowableDistinctUntilChanged<T>(source, comparer);
        }

        /// <summary>
        /// Relays elements from the source IFlowable until the other IPublisher signals an item
        /// or completes.
        /// </summary>
        /// <typeparam name="T">The source value type.</typeparam>
        /// <typeparam name="U">The other element type.</typeparam>
        /// <param name="source">The source IFlowable instance.</param>
        /// <param name="other">The other IPublisher instance.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<T> TakeUntil<T, U>(this IFlowable<T> source, IPublisher<U> other)
        {
            return new FlowableTakeUntil<T, U>(source, other);
        }

        /// <summary>
        /// Skips items from the source IFlowable until the other IPublisher signals an item
        /// or completes.
        /// </summary>
        /// <typeparam name="T">The source value type.</typeparam>
        /// <typeparam name="U">The other value type.</typeparam>
        /// <param name="source">The source IFlowable instance.</param>
        /// <param name="other">The other IPublisher instance.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<T> SkipUntil<T, U>(this IFlowable<T> source, IPublisher<U> other)
        {
            return new FlowableSkipUntil<T, U>(source, other);
        }

        /// <summary>
        /// Converts a downstream IFlowableSubscriber into an IFlowableSubscriber to be subscribed
        /// to the upstream source IFlowable.
        /// </summary>
        /// <typeparam name="T">The upstream value type.</typeparam>
        /// <typeparam name="R">The downstream value type.</typeparam>
        /// <param name="source">The source IFlowable instance.</param>
        /// <param name="lifter">The function that receives the downstream IFlowableSubscriber
        /// and returns an IFlowableSubscriber for the upstream.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<R> Lift<T, R>(this IFlowable<T> source, Func<IFlowableSubscriber<R>, IFlowableSubscriber<T>> lifter)
        {
            return new FlowableLift<T, R>(source, lifter);
        }

        /// <summary>
        /// Invokes the composer function with the given source IFlowable
        /// in a fluent fashion.
        /// </summary>
        /// <typeparam name="T">The source value type.</typeparam>
        /// <typeparam name="R">The result value type.</typeparam>
        /// <param name="source">The source IFlowable instance.</param>
        /// <param name="composer">The function called with the source IFlowable
        /// and should produce an IPublisher as a result.</param>
        /// <returns>The result of the function call.</returns>
        public static IFlowable<R> Compose<T, R>(this IFlowable<T> source, Func<IFlowable<T>, IPublisher<R>> composer)
        {
            return composer(source).ToFlowable();
        }

        /// <summary>
        /// Converts the source IFlowable into a value via the given converter function
        /// to allow fluent conversions.
        /// </summary>
        /// <typeparam name="T">The input value type.</typeparam>
        /// <typeparam name="R">The result value type.</typeparam>
        /// <param name="source">The source IFlowable instance.</param>
        /// <param name="converter">The function that takes the source IFlowable
        /// and returns a value to be returned by this operator.</param>
        /// <returns>The value returned by the converter function.</returns>
        public static R To<T, R>(this IFlowable<T> source, Func<IFlowable<T>, R> converter)
        {
            return converter(source);
        }

        /// <summary>
        /// Defers the call to the composer function to subscription time,
        /// allowing a per-Subscriber state to be created.
        /// </summary>
        /// <typeparam name="T">The input value type.</typeparam>
        /// <typeparam name="R">The result value type.</typeparam>
        /// <param name="source">The source IFlowable instance.</param>
        /// <param name="composer">The function that receives the upstream source and
        /// should return the IPublisher to be subscribed to by the downstream.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<R> DeferCompose<T, R>(this IFlowable<T> source, Func<IFlowable<T>, IPublisher<R>> composer)
        {
            return Defer(() => composer(source));
        }

        /// <summary>
        /// Switches to emitting the values of the latest IPublisher mapped from
        /// the upstream value, cancelling the previous IPublisher in the process.
        /// </summary>
        /// <typeparam name="T">The input value type.</typeparam>
        /// <typeparam name="R">The result value type.</typeparam>
        /// <param name="source">The source IFlowable instance.</param>
        /// <param name="mapper">The function that receives an item from upstream
        /// and turns it into an IPublisher whose elements should be relayed then on.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<R> SwitchMap<T, R>(this IFlowable<T> source, Func<T, IPublisher<R>> mapper)
        { 
            return SwitchMap(source, mapper, BufferSize());
        }

        /// <summary>
        /// Switches to emitting the values of the latest IPublisher mapped from
        /// the upstream value, cancelling the previous IPublisher in the process.
        /// </summary>
        /// <typeparam name="T">The input value type.</typeparam>
        /// <typeparam name="R">The result value type.</typeparam>
        /// <param name="source">The source IFlowable instance.</param>
        /// <param name="mapper">The function that receives an item from upstream
        /// and turns it into an IPublisher whose elements should be relayed then on.</param>
        /// <param name="prefetch">The number of items to prefetch from each inner IPublisher.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<R> SwitchMap<T, R>(this IFlowable<T> source, Func<T, IPublisher<R>> mapper, int prefetch)
        {
            return new FlowableSwitchMap<T, R>(source, mapper, prefetch);
        }

        /// <summary>
        /// Emits the default item if the source is empty.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The source IFlowable instance.</param>
        /// <param name="defaultItem">The item to emit if the source is empty.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<T> DefaultIfEmpty<T>(this IFlowable<T> source, T defaultItem)
        {
            return new FlowableDefaultIfEmpty<T>(source, defaultItem);
        }

        /// <summary>
        /// Switches to the fallback IPublisher if the source IFlowable is empty.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The source IFlowable instance.</param>
        /// <param name="fallback">The fallback IPublisher if the source turns out to be empty.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<T> SwitchIfEmpty<T>(this IFlowable<T> source, IPublisher<T> fallback)
        {
            return new FlowableSwitchIfEmpty<T>(source, fallback);
        }

        /// <summary>
        /// Switches to the next fallback IPublisher if the main source or the previous one is empty.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The source IFlowable instance.</param>
        /// <param name="fallbacks">The params array of fallback IPublishers.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<T> SwitchIfEmpty<T>(this IFlowable<T> source, params IPublisher<T>[] fallbacks)
        {
            return new FlowableSwitchIfEmptyArray<T>(source, fallbacks);
        }

        /// <summary>
        /// Switches to the next fallback IPublisher if the main source or the previous one is empty.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The source IFlowable instance.</param>
        /// <param name="fallbacks">The enumerable sequence of fallback IPublishers.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<T> SwitchIfEmpty<T>(this IFlowable<T> source, IEnumerable<IPublisher<T>> fallbacks)
        {
            return new FlowableSwitchIfEmptyEnumerable<T>(source, fallbacks);
        }

        /// <summary>
        /// Repeatedly subscribes to the source IFlowable up to an optional maximum number of times.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The source IFlowable instance.</param>
        /// <param name="times">The number of times to subscribe to the source.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<T> Repeat<T>(this IFlowable<T> source, long times = long.MaxValue)
        {
            return Repeat(source, AlwaysFalse.Instance, times);
        }

        /// <summary>
        /// Repeatedly subscribes to the source IFlowable up to an optional maximum number of times or
        /// when the stop function returns true after the current subscription completes.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The source IFlowable instance.</param>
        /// <param name="stop">The function that should return true to stop repeating the whole sequence.</param>
        /// <param name="times">The number of times to subscribe to the source.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<T> Repeat<T>(this IFlowable<T> source, Func<bool> stop, long times = long.MaxValue)
        {
            if (times <= 0)
            {
                return Empty<T>();
            }
            return new FlowableRepeat<T>(source, stop, times);
        }

        /// <summary>
        /// Repeats the source if the IPublisher returned by the handler function signals
        /// an item in response to the completion of the source. 
        /// If that IPublisher signals a terminal event, the downstream is terminated
        /// with the same terminal event.
        /// </summary>
        /// <typeparam name="T">The source and result value type.</typeparam>
        /// <typeparam name="U">The handler's item type (not relevant)</typeparam>
        /// <param name="source">The source IFlowable instance.</param>
        /// <param name="handler">The handler function which receives an IFlowable
        /// which emits an item if the source completed normally and should return an IPublisher
        /// that emits an item at some point in time in response to indicate repeat should happen.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<T> RepeatWhen<T, U>(this IFlowable<T> source, Func<IFlowable<object>, IPublisher<U>> handler)
        {
            return new FlowableRepeatWhen<T, U>(source, handler);
        }

        /// <summary>
        /// Resubscribes to the source if it fails with an exception, optionally only
        /// a limited number of times.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The source IFlowable instance.</param>
        /// <param name="times">The number of times to retry.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<T> Retry<T>(this IFlowable<T> source, long times = long.MaxValue)
        {
            return Retry<T>(source, AlwaysTrue<Exception>.Instance, times);
        }

        /// <summary>
        /// Resubscribes to the source if it fails with an exception, optionally only
        /// a limited number of times.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The source IFlowable instance.</param>
        /// <param name="predicate">Called with the exception from the source and if
        /// returns true, the source is resubscribed.</param>
        /// <param name="times">The maximum number of times to retry.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<T> Retry<T>(this IFlowable<T> source, Func<Exception, bool> predicate, long times = long.MaxValue)
        {
            return new FlowableRetry<T>(source, predicate, times);
        }

        /// <summary>
        /// Retries the source if the IPublisher returned by the handler function signals
        /// an item in response to the Exception the source emitted. 
        /// If that IPublisher signals a terminal event, the downstream is terminated
        /// with the same terminal event.
        /// </summary>
        /// <typeparam name="T">The source and result value type.</typeparam>
        /// <typeparam name="U">The handler's item type (not relevant)</typeparam>
        /// <param name="source">The source IFlowable instance.</param>
        /// <param name="handler">The handler function which receives an IFlowable
        /// which emits the Exception that caused the source failure and should return an IPublisher
        /// that emits an item at some point in time in response to indicate retry should happen.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<T> RetryWhen<T, U>(this IFlowable<T> source, Func<IFlowable<Exception>, IPublisher<U>> handler)
        {
            return new FlowableRetryWhen<T, U>(source, handler);
        }

        /// <summary>
        /// Emits the given fallback item and completes when the source signals an error.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The source IFlowable instance.</param>
        /// <param name="fallbackItem">The item to emit and complete instead of signalling an error.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<T> OnErrorReturn<T>(this IFlowable<T> source, T fallbackItem)
        {
            return new FlowableOnErrorReturn<T>(source, fallbackItem);
        }

        /// <summary>
        /// Completes the sequence instead of singalling the upstream error.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The source IFlowable instance.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<T> OnErrorComplete<T>(this IFlowable<T> source)
        {
            return new FlowableOnErrorComplete<T>(source);
        }

        /// <summary>
        /// If the upstream terminates with an error, the sequence is continued with
        /// the given IPublisher's signals.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The source IFlowable instance.</param>
        /// <param name="fallback">The fallback IPublisher to continue with if the
        /// source fails.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<T> OnErrorResumeNext<T>(this IFlowable<T> source, IPublisher<T> fallback)
        {
            return OnErrorResumeNext(source, e => fallback);
        }

        /// <summary>
        /// If the upstream terminates with an error, the handler function is called
        /// with the Exception instance which returns an IPublisher to continue with.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The source IFlowable instance.</param>
        /// <param name="handler">The function that receives the upstream's error
        /// and should return an IPublisher to continue with.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<T> OnErrorResumeNext<T>(this IFlowable<T> source, Func<Exception, IPublisher<T>> handler)
        {
            return new FlowableOnErrorResumeNext<T>(source, handler);
        }

        /// <summary>
        /// Switches to the fallback IPublisher or signals a TimeoutException if the next item from
        /// the source IFlowable doesn't arrive within te specified itemTimeout.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The source IFlowable instance</param>
        /// <param name="itemTimeout">The timeout value for the first and subsequent items relative
        /// to the last emission.</param>
        /// <param name="fallback">The optional IPublisher to switch to if there is a timeout, if
        /// null, a TimeoutException is signalled instead.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<T> Timeout<T>(this IFlowable<T> source, TimeSpan itemTimeout, IPublisher<T> fallback = null)
        {
            return Timeout(source, itemTimeout, itemTimeout, Executors.Computation, fallback);
        }

        /// <summary>
        /// Switches to the fallback IPublisher or signals a TimeoutException if the next item from
        /// the source IFlowable doesn't arrive within te specified itemTimeout.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The source IFlowable instance</param>
        /// <param name="itemTimeout">The timeout value for the first and subsequent items relative
        /// to the last emission.</param>
        /// <param name="executor">The IExecutorService where the wait should happen.</param>
        /// <param name="fallback">The optional IPublisher to switch to if there is a timeout, if
        /// null, a TimeoutException is signalled instead.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<T> Timeout<T>(this IFlowable<T> source, TimeSpan itemTimeout, IExecutorService executor, IPublisher<T> fallback = null)
        {
            return Timeout(source, itemTimeout, itemTimeout, executor, fallback);
        }

        /// <summary>
        /// Switches to the fallback IPublisher or signals a TimeoutException if the next item from
        /// the source IFlowable doesn't arrive within te specified itemTimeout.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The source IFlowable instance</param>
        /// <param name="firstTimeout">The timeout for the first item relative to when the subscription
        /// happened.</param>
        /// <param name="itemTimeout">The timeout value for the subsequent items relative
        /// to the last emission.</param>
        /// <param name="fallback">The optional IPublisher to switch to if there is a timeout, if
        /// null, a TimeoutException is signalled instead.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<T> Timeout<T>(this IFlowable<T> source, TimeSpan firstTimeout, TimeSpan itemTimeout, IPublisher<T> fallback = null)
        {
            return Timeout(source, firstTimeout, itemTimeout, Executors.Computation, fallback);
        }

        /// <summary>
        /// Switches to the fallback IPublisher or signals a TimeoutException if the next item from
        /// the source IFlowable doesn't arrive within te specified itemTimeout.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The source IFlowable instance</param>
        /// <param name="firstTimeout">The timeout for the first item relative to when the subscription
        /// happened.</param>
        /// <param name="itemTimeout">The timeout value for the subsequent items relative
        /// to the last emission.</param>
        /// <param name="executor">The IExecutorService where the wait should happen.</param>
        /// <param name="fallback">The optional IPublisher to switch to if there is a timeout, if
        /// null, a TimeoutException is signalled instead.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<T> Timeout<T>(this IFlowable<T> source, TimeSpan firstTimeout, TimeSpan itemTimeout, IExecutorService executor, IPublisher<T> fallback = null)
        {
            return new FlowableTimeout<T>(source, firstTimeout, itemTimeout, executor, fallback);
        }

        /// <summary>
        /// Consumes the upstream source in an unbounded manner and
        /// signals InvalidOperationException if the downstream can't
        /// keep up.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The source IFlowable instance.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<T> OnBackpressureError<T>(this IFlowable<T> source)
        {
            return new FlowableOnBackpressureError<T>(source);
        }

        /// <summary>
        /// Consumes the upstream source in an unbounded manner and
        /// drops items (calling the onDrop action if available)
        /// if the downstream can't keep up.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The source IFlowable instance.</param>
        /// <param name="onDrop">The optional action called when an item is dropped.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<T> OnBackpressureDrop<T>(this IFlowable<T> source, Action<T> onDrop = null)
        {
            return new FlowableOnBackpressureDrop<T>(source, onDrop);
        }

        /// <summary>
        /// Consumes the upstream source in an unbounded fashion and and the
        /// downstream picks up the latest available item on its own pace.
        /// The provided Action will be called with the unclaimed items.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="source">The source IFlowable instance.</param>
        /// <param name="onDrop">The action called with the unclaimed items.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<T> OnBackpressureLatest<T>(this IFlowable<T> source, Action<T> onDrop = null)
        {
            return new FlowableOnBackpressureLatest<T>(source, onDrop);
        }

        /// <summary>
        /// Consumes the upstream source in an unbounded manner and
        /// buffers all items if the downstream can't keep up.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The source IFlowable instance.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<T> OnBackpressureBuffer<T>(this IFlowable<T> source)
        {
            return OnBackpressureBuffer(source, BufferSize(), BufferStrategy.ALL, null);
        }

        /// <summary>
        /// Consumes the upstream source in an unbounded manner into a potentially
        /// bounded buffer and evicts items based on a buffer strategy and the
        /// evicted item can be optionally consumed via an Action.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The source IFlowable instance.</param>
        /// <param name="capacityHint">The number of items to keep in the buffer in case the
        /// buffer strategy is not ALL, otherwise it is a hint on how to extend the internal buffer.</param>
        /// <param name="strategy">The strategy determining what should happen in case of a buffer size
        /// reaching the capacity hint.</param>
        /// <param name="onDrop">The optional Action called with the item evicted from the buffer.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<T> OnBackpressureBuffer<T>(this IFlowable<T> source, int capacityHint, BufferStrategy strategy = BufferStrategy.ALL, Action<T> onDrop = null)
        {
            if (strategy == BufferStrategy.ALL)
            {
                return new FlowableOnBackpressureBufferAll<T>(source, capacityHint);
            }
            return new FlowableOnBackpressureBuffer<T>(source, capacityHint, strategy, onDrop);
        }

        /// <summary>
        /// Emits upstream items into an exclusive group represented by an IGroupedFlowable
        /// based on a key.
        /// </summary>
        /// <typeparam name="T">The source value type.</typeparam>
        /// <typeparam name="K">The key type.</typeparam>
        /// <param name="source">The source IFlowable instance.</param>
        /// <param name="keyMapper">The function that takes an upstream item
        /// and returns a key value that selects a group to emit the upstream item.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<IGroupedFlowable<K, T>> GroupBy<T, K>(this IFlowable<T> source, Func<T, K> keyMapper)
        {
            return GroupBy(source, keyMapper, Identity<T>.Instance, BufferSize());
        }

        /// <summary>
        /// Emits a value mapped from each upstream item into an exclusive group represented by an IGroupedFlowable
        /// based on a key.
        /// </summary>
        /// <typeparam name="T">The source value type.</typeparam>
        /// <typeparam name="K">The key type.</typeparam>
        /// <typeparam name="V">The group value type.</typeparam>
        /// <param name="source">The source IFlowable instance.</param>
        /// <param name="keyMapper">The function that takes an upstream item
        /// and returns a key value that selects a group to emit the upstream item.</param>
        /// <param name="valueMapper">The function that takes the upstream value and
        /// transforms it into the value to be emitted in the selected group.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<IGroupedFlowable<K, V>> GroupBy<T, K, V>(this IFlowable<T> source, Func<T, K> keyMapper, Func<T, V> valueMapper)
        {
            return GroupBy(source, keyMapper, valueMapper, BufferSize());
        }
        /// <summary>
        /// Emits a value mapped from each upstream item into an exclusive group represented by an IGroupedFlowable
        /// based on a key.
        /// </summary>
        /// <typeparam name="T">The source value type.</typeparam>
        /// <typeparam name="K">The key type.</typeparam>
        /// <typeparam name="V">The group value type.</typeparam>
        /// <param name="source">The source IFlowable instance.</param>
        /// <param name="keyMapper">The function that takes an upstream item
        /// and returns a key value that selects a group to emit the upstream item.</param>
        /// <param name="valueMapper">The function that takes the upstream value and
        /// transforms it into the value to be emitted in the selected group.</param>
        /// <param name="bufferSize">The buffer size for the main groups and each group.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<IGroupedFlowable<K, V>> GroupBy<T, K, V>(this IFlowable<T> source, Func<T, K> keyMapper, Func<T, V> valueMapper, int bufferSize)
        {
            return new FlowableGroupBy<T, K, V>(source, keyMapper, valueMapper, bufferSize);
        }

        /// <summary>
        /// Combines the current latest element from the other IPublisher with the source
        /// via a combiner function to produce the output item.
        /// </summary>
        /// <typeparam name="T">The source value type.</typeparam>
        /// <typeparam name="U">The other value type.</typeparam>
        /// <typeparam name="R">The result value type.</typeparam>
        /// <param name="source">The source IFlowable instance.</param>
        /// <param name="other">The other IPublisher instance.</param>
        /// <param name="combiner">The function receiving each item from the source
        /// and uses the latest item from the other IPublisher to return a value
        /// to be emitted.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<R> WithLatestFrom<T, U, R>(this IFlowable<T> source, IPublisher<U> other, Func<T, U, R> combiner)
        {
            return new FlowableWithLatestFrom<T, U, R>(source, other, combiner);
        }

        /// <summary>
        /// Combines the current latest element from the array of other IPublishers with the source
        /// via a combiner function to produce the output item.
        /// </summary>
        /// <typeparam name="T">The source value type.</typeparam>
        /// <typeparam name="R">The result value type.</typeparam>
        /// <param name="source">The source IFlowable instance.</param>
        /// <param name="others">The params array of other IPublisher instances.</param>
        /// <param name="combiner">The function receiving each item from the source
        /// and uses the latest item from the other IPublisher to return a value
        /// to be emitted.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<R> WithLatestFrom<T, R>(this IFlowable<T> source, Func<T[], R> combiner, params IPublisher<T>[] others)
        {
            return new FlowableWithLatestFromArray<T, R>(source, others, combiner);
        }

        /// <summary>
        /// Combines the current latest element from the enumerable sequence of other IPublishers with the source
        /// via a combiner function to produce the output item.
        /// </summary>
        /// <typeparam name="T">The source value type.</typeparam>
        /// <typeparam name="R">The result value type.</typeparam>
        /// <param name="source">The source IFlowable instance.</param>
        /// <param name="others">The IEnumerable sequence of other IPublisher instances.</param>
        /// <param name="combiner">The function receiving each item from the source
        /// and uses the latest item from the other IPublisher to return a value
        /// to be emitted.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<R> WithLatestFrom<T, R>(this IFlowable<T> source, IEnumerable<IPublisher<T>> others, Func<T[], R> combiner)
        {
            return new FlowableWithLatestFromEnumerable<T, R>(source, others, combiner);
        }

        /// <summary>
        /// Periodically samples the latest value of the source and emits this item, optionally
        /// emitting the very last item if the source completes before the next period.
        /// </summary>
        /// <typeparam name="T">The source value type.</typeparam>
        /// <param name="source">The source IFlowable instance.</param>
        /// <param name="period">The sampling period.</param>
        /// <param name="emitLast">If true, the very last not-yet sampled item is emitted before completion.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<T> Sample<T>(this IFlowable<T> source, TimeSpan period, bool emitLast = true)
        {
            return Sample(source, Interval(period), emitLast);
        }

        /// <summary>
        /// Periodically samples the latest value, from the given executor, of the source and emits this item, optionally
        /// emitting the very last item if the source completes before the next period.
        /// </summary>
        /// <typeparam name="T">The source value type.</typeparam>
        /// <param name="source">The source IFlowable instance.</param>
        /// <param name="period">The sampling period.</param>
        /// <param name="executor">The IExecutorService to use for the timed sampling.</param>
        /// <param name="emitLast">If true, the very last not-yet sampled item is emitted before completion.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<T> Sample<T>(this IFlowable<T> source, TimeSpan period, IExecutorService executor, bool emitLast = true)
        {
            return Sample(source, Interval(period, executor), emitLast);
        }
        /// <summary>
        /// Emits the latest value of the upstream source when the sampler IPublisher signals an item,
        /// optionally emitting the last item if the upstream terminates before the sampler IPublisher
        /// signals its next item. If the sampler IPublisher terminates, the sequence whole terminates.
        /// </summary>
        /// <typeparam name="T">The upstream and result value type.</typeparam>
        /// <typeparam name="U">The element type of the other IPublisher (not relevant).</typeparam>
        /// <param name="source">The source IFlowable instance.</param>
        /// <param name="sampler">The IPublisher whose items trigger a sampling and emission
        /// of the latest item of the upstream source.</param>
        /// <param name="emitLast">If true, the latest item from upstream is emitted when
        /// the upstream or sampler terminates.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<T> Sample<T, U>(this IFlowable<T> source, IPublisher<U> sampler, bool emitLast = true)
        {
            return new FlowableSample<T, U>(source, sampler, emitLast);
        }

        /// <summary>
        /// Emits the latest item if there are no newer items from upstream arriving within
        /// the delay window. Otherwise the grace period start over with the new item.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The source IFlowable instance.</param>
        /// <param name="delay">The grace period where no newer items should arrive from upstream
        /// in order to emit the latest item.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<T> Debounce<T>(this IFlowable<T> source, TimeSpan delay)
        {
            return Debounce(source, delay, Executors.Computation);
        }

        /// <summary>
        /// Emits the latest item if there are no newer items from upstream arriving within
        /// the delay window. Otherwise the grace period start over with the new item.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The source IFlowable instance.</param>
        /// <param name="delay">The grace period where no newer items should arrive from upstream
        /// in order to emit the latest item.</param>
        /// <param name="executor">The IExecutorService to use for the timing and emission</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<T> Debounce<T>(this IFlowable<T> source, TimeSpan delay, IExecutorService executor)
        {
            return new FlowableDebounce<T>(source, delay, executor);
        }

        /// <summary>
        /// Emits an item and then blocks out subsequent upstream items for the specified delay duration.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The source IFlowable instance.</param>
        /// <param name="delay">The length of the time window after the the next item will
        /// start the next blockout window.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<T> ThrottleFirst<T>(this IFlowable<T> source, TimeSpan delay)
        {
            return ThrottleFirst(source, delay, Executors.Computation);
        }

        /// <summary>
        /// Emits an item and then blocks out subsequent upstream items for the specified delay duration.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The source IFlowable instance.</param>
        /// <param name="delay">The length of the time window after the the next item will
        /// start the next blockout window.</param>
        /// <param name="executor">The Executor to take timing information from; the
        /// the first item in a window is emitted on the same thread of the upstream.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<T> ThrottleFirst<T>(this IFlowable<T> source, TimeSpan delay, IExecutorService executor)
        {
            return new FlowableThrottleFirst<T>(source, delay, executor);
        }

        /// <summary>
        /// Emits the latest value from upstream at the specified time intervals.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The source IFlowable instance.</param>
        /// <param name="delay">The periodicity at which the latest item should be emitted
        /// (each such item only once).</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<T> ThrottleLast<T>(this IFlowable<T> source, TimeSpan delay)
        {
            return Sample(source, delay, Executors.Computation);
        }

        /// <summary>
        /// Emits the latest value from upstream at the specified time intervals.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The source IFlowable instance.</param>
        /// <param name="delay">The periodicity at which the latest item should be emitted
        /// (each such item only once).</param>
        /// <param name="executor">The IExecutorService to use for the timing and emission</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<T> ThrottleLast<T>(this IFlowable<T> source, TimeSpan delay, IExecutorService executor)
        {
            return Sample(source, delay, executor);
        }

        /// <summary>
        /// Emits the latest item if there are no newer items from upstream arriving within
        /// the delay window. Otherwise the grace period start over with the new item.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The source IFlowable instance.</param>
        /// <param name="delay">The grace period where no newer items should arrive from upstream
        /// in order to emit the latest item.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<T> ThrottleWithTimeout<T>(this IFlowable<T> source, TimeSpan delay)
        {
            return Debounce(source, delay, Executors.Computation);
        }

        /// <summary>
        /// Emits the latest item if there are no newer items from upstream arriving within
        /// the delay window. Otherwise the grace period start over with the new item.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The source IFlowable instance.</param>
        /// <param name="delay">The grace period where no newer items should arrive from upstream
        /// in order to emit the latest item.</param>
        /// <param name="executor">The IExecutorService to use for the timing and emission</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<T> ThrottleWithTimeout<T>(this IFlowable<T> source, TimeSpan delay, IExecutorService executor)
        {
            return Debounce(source, delay, executor);
        }

        /// <summary>
        /// Buffers elements into non-overlapping ILists with the given maximum size.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The source IFlowable instance.</param>
        /// <param name="size">The maximum number of elements in each list.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<IList<T>> Buffer<T>(this IFlowable<T> source, int size)
        {
            return Buffer(source, size, ListSupplier<T>.Instance);
        }

        /// <summary>
        /// Buffers elements into non-overlapping ICollection provided by a supplier function
        /// and each such collection receives at most the given number of items.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <typeparam name="C">The subtype that collects the items.</typeparam>
        /// <param name="source">The source IFlowable instance.</param>
        /// <param name="size">The maximum number of elements in each list.</param>
        /// <param name="collectionSupplier">The collection supplier to provide fresh buffers.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<C> Buffer<T, C>(this IFlowable<T> source, int size, Func<C> collectionSupplier) where C : ICollection<T>
        {
            return new FlowableBufferSizeExact<T, C>(source, size, collectionSupplier);
        }

        /// <summary>
        /// Buffers items into possible overlapping ILists.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The source IFlowable instance.</param>
        /// <param name="size">The number of items to put into each buffer</param>
        /// <param name="skip">Multiples of skip will start a fresh buffer, anything
        /// between will be dropped.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<IList<T>> Buffer<T>(this IFlowable<T> source, int size, int skip)
        {
            return Buffer(source, size, skip, ListSupplier<T>.Instance);
        }

        /// <summary>
        /// Buffers items into possible overlapping ILists.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <typeparam name="C">The subtype that collects the items.</typeparam>
        /// <param name="source">The source IFlowable instance.</param>
        /// <param name="size">The number of items to put into each buffer</param>
        /// <param name="skip">Multiples of skip will start a fresh buffer, anything
        /// between will be dropped.</param>
        /// <param name="collectionSupplier">The collection supplier to provide fresh buffers.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<C> Buffer<T, C>(this IFlowable<T> source, int size, int skip, Func<C> collectionSupplier) where C : ICollection<T>
        {
            if (size == skip)
            {
                return Buffer(source, size, collectionSupplier);
            }
            else
            if (size < skip)
            {
                return new FlowableBufferSizeSkip<T, C>(source, size, skip, collectionSupplier);
            }
            return new FlowableBufferSizeOverlap<T, C>(source, size, skip, collectionSupplier);
        }

        public static IFlowable<IList<T>> Buffer<T, U>(this IFlowable<T> source, IPublisher<U> boundary)
        {
            return Buffer(source, boundary, ListSupplier<T>.Instance);
        }

        public static IFlowable<C> Buffer<T, U, C>(this IFlowable<T> source, IPublisher<U> boundary, Func<C> collectionSupplier) where C : ICollection<T>
        {
            // TODO implement
            throw new NotImplementedException();
        }

        /// <summary>
        /// Splits up the source IFlowable into consecutive, non-overlapping sub IFlowable windows
        /// of the given size (the last one might be shorter).
        /// </summary>
        /// <typeparam name="T">The value type of the upstream and the windows.</typeparam>
        /// <param name="source">The source IFlowable instance.</param>
        /// <param name="size">The maximum number of items in each window.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<IFlowable<T>> Window<T>(this IFlowable<T> source, int size)
        {
            return new FlowableWindowSizeExact<T>(source, size);
        }

        /// <summary>
        /// Splits up the source IFlowable into potentially skipping or overlapping sub IFlowable
        /// windows with the given size.
        /// </summary>
        /// <typeparam name="T">The value type of the upstream and the windows.</typeparam>
        /// <param name="source">The source IFlowable instance.</param>
        /// <param name="size">The maximum number of items in each window.</param>
        /// <param name="skip">The number of items to skip before starting a new window.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<IFlowable<T>> Window<T>(this IFlowable<T> source, int size, int skip)
        {
            if (size == skip)
            {
                return Window(source, size);
            }
            if (size < skip)
            {
                return new FlowableWindowSizeSkip<T>(source, size, skip);
            }
            return new FlowableWindowSizeOverlap<T>(source, size, skip);
        }

        public static IFlowable<IFlowable<T>> Window<T, U>(this IFlowable<T> source, IPublisher<U> boundary)
        {
            // TODO implement
            throw new NotImplementedException();
        }

        /// <summary>
        /// Performs a rolling accumulation of items via the scanner function where the accumulator
        /// starts with the first upstream item and the scanner takes the previous accumulator item
        /// and the current upstream item to return a new accumulator value to be used later and
        /// also emitted to downstream.
        /// </summary>
        /// <typeparam name="T">The source and output value type.</typeparam>
        /// <param name="source">The source IFlowable instance.</param>
        /// <param name="scanner">The function called once at least two upstream items have been
        /// received when the function receives the previous accumulator value (or the very
        /// first upstream value) and the current upstream value and should return the value
        /// to be emitted and to become the new accumulator value.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<T> Scan<T>(this IFlowable<T> source, Func<T, T, T> scanner)
        {
            return new FlowableScan<T>(source, scanner);
        }

        /// <summary>
        /// Performs a rolling accumulation of items, starting from an initial accumulator value
        /// and applying a scanner function to the current accumulator value and the current upstream
        /// value to get the next accumulator value and the item emitted to downstream.
        /// </summary>
        /// <typeparam name="T">The source value type.</typeparam>
        /// <typeparam name="R">The accumulator and result value type.</typeparam>
        /// <param name="source">The source IFlowable instance.</param>
        /// <param name="initialSupplier">The function returning the fresh initial accumulator value for
        /// each ISubscriber.</param>
        /// <param name="scanner">The function called with the initial or current accumulator value
        /// and the current upstream value and should return the new accumulator value.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<R> Scan<T, R>(this IFlowable<T> source, Func<R> initialSupplier, Func<R, T, R> scanner)
        {
            return Scan<T, R>(source, initialSupplier, scanner, BufferSize());
        }

        /// <summary>
        /// Performs a rolling accumulation of items, starting from an initial accumulator value
        /// and applying a scanner function to the current accumulator value and the current upstream
        /// value to get the next accumulator value and the item emitted to downstream.
        /// </summary>
        /// <typeparam name="T">The source value type.</typeparam>
        /// <typeparam name="R">The accumulator and result value type.</typeparam>
        /// <param name="source">The source IFlowable instance.</param>
        /// <param name="initialSupplier">The function returning the fresh initial accumulator value for
        /// each ISubscriber.</param>
        /// <param name="scanner">The function called with the initial or current accumulator value
        /// and the current upstream value and should return the new accumulator value.</param>
        /// <param name="bufferSize">The number of items to store and prefetch from upstream.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<R> Scan<T, R>(this IFlowable<T> source, Func<R> initialSupplier, Func<R, T, R> scanner, int bufferSize)
        {
            return new FlowableScanWith<T, R>(source, initialSupplier, scanner, bufferSize);
        }

        /// <summary>
        /// Relays items of the source or the other IFlowable, whichever signals first.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The source IFlowable instance.</param>
        /// <param name="other">The other IFlowable instance.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<T> AmbWith<T>(this IFlowable<T> source, IPublisher<T> other)
        {
            return Amb(source, other);
        }

        /// <summary>
        /// Concatenates the source sequence with the other IPublisher in order and in a
        /// non-overlapping fashion.
        /// </summary>
        /// <typeparam name="T">The common value type.</typeparam>
        /// <param name="source">The source IFlowable instance.</param>
        /// <param name="other">The other IPublisher instance.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<T> ConcatWith<T>(this IFlowable<T> source, IPublisher<T> other)
        {
            return Concat(source, other);
        }

        /// <summary>
        /// Merges the source sequence with the other IPublisher without any guarantee of
        /// item ordering between the two.
        /// </summary>
        /// <typeparam name="T">THe common value type.</typeparam>
        /// <param name="source">The source IFlowable instance.</param>
        /// <param name="other">The other IPublisher instance.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<T> MergeWith<T>(this IFlowable<T> source, IPublisher<T> other)
        {
            return Merge(source, other);
        }

        /// <summary>
        /// Pairwise combines elements from the source and other sequence via a zipper function.
        /// </summary>
        /// <typeparam name="T">The source element type.</typeparam>
        /// <typeparam name="U">The other element type.</typeparam>
        /// <typeparam name="R">The result value type.</typeparam>
        /// <param name="source">The source IFlowable instance.</param>
        /// <param name="other">The other IPublisher instance.</param>
        /// <param name="zipper">The function that receives one element from the source and
        /// other sequences and returns their combination as result.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<R> ZipWith<T, U, R>(this IFlowable<T> source, IPublisher<U> other, Func<T, U, R> zipper)
        {
            return Zip(source, other, zipper);
        }

        /// <summary>
        /// Maps the upstream values onto IEnumerables and relays the items in them in a sequential manner.
        /// </summary>
        /// <typeparam name="T">The source value type.</typeparam>
        /// <typeparam name="R">The result value type.</typeparam>
        /// <param name="source">The source IFlowable instance.</param>
        /// <param name="mapper">The function that maps items to IEnumerables.</param>
        /// <returns>The new Flowable instance.</returns>
        public static IFlowable<R> FlatMapEnumerable<T, R>(this IFlowable<T> source, Func<T, IEnumerable<R>> mapper)
        {
            return FlatMapEnumerable(source, mapper, BufferSize());
        }

        /// <summary>
        /// Maps the upstream values onto IEnumerables and relays the items in them in a sequential manner.
        /// </summary>
        /// <typeparam name="T">The source value type.</typeparam>
        /// <typeparam name="R">The result value type.</typeparam>
        /// <param name="source">The source IFlowable instance.</param>
        /// <param name="mapper">The function that maps items to IEnumerables.</param>
        /// <param name="prefetch">The number of items to prefetch from the upstream.</param>
        /// <returns>The new Flowable instance.</returns>
        public static IFlowable<R> FlatMapEnumerable<T, R>(this IFlowable<T> source, Func<T, IEnumerable<R>> mapper, int prefetch)
        {
            return new FlowableFlatMapEnumerable<T, R>(source, mapper, prefetch);
        }

        /// <summary>
        /// Takes elements from upstream while the predicate returns true for the
        /// current item.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The source IFlowable instance.</param>
        /// <param name="predicate">The function called with the current item and should
        /// return true for emitting and continuing or false for ending the sequence.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<T> TakeWhile<T>(this IFlowable<T> source, Func<T, bool> predicate)
        {
            return new FlowableTakeWhile<T>(source, predicate);
        }

        /// <summary>
        /// Takes items from upstream until the predicate returns true after the
        /// current item.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The source IFlowable instance.</param>
        /// <param name="predicate">The predicate that receives the current item
        /// and should return true to stop the sequence.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<T> TakeUntil<T>(this IFlowable<T> source, Func<T, bool> predicate)
        {
            return new FlowableTakeUntilPredicate<T>(source, predicate);
        }

        /// <summary>
        /// Skips the upstream items while the predicate returns true
        /// then relays the remaining items.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The source IFlowable instance.</param>
        /// <param name="predicate">The function that takes the current item and returns
        /// true to skip it or false to relay it and all subsequent items.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<T> SkipWhile<T>(this IFlowable<T> source, Func<T, bool> predicate)
        {
            return new FlowableSkipWhile<T>(source, predicate);
        }

        // ********************************************************************************
        // IConnectableFlowable related
        // ********************************************************************************

        /// <summary>
        /// Multicasts upstream items from upstream to multiple ISubscribers by sharing
        /// the same underlying subscription to upstream. Items are emitted in a lockstep
        /// fashion, in other words, when all currently subscribed ISubscribers are ready
        /// to receive. Call Connect() to establish a connection.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="source">The upstream IFlowable source.</param>
        /// <returns>The new IConnectableFlowable instance.</returns>
        public static IConnectableFlowable<T> Publish<T>(this IFlowable<T> source)
        {
            return Multicast(source, PublishProcessorSupplier<T>.Instance);
        }

        /// <summary>
        /// Multicasts upstream items from upstream to multiple ISubscribers by sharing
        /// the same underlying subscription to upstream. Items are emitted in a lockstep
        /// fashion, in other words, when all currently subscribed ISubscribers are ready
        /// to receive. Call Connect() to establish a connection.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="source">The upstream IFlowable source.</param>
        /// <param name="bufferSize">The number of items to prefetch and keep buffered from upstream.</param>
        /// <returns>The new IConnectableFlowable instance.</returns>
        public static IConnectableFlowable<T> Publish<T>(this IFlowable<T> source, int bufferSize)
        {
            return Multicast(source, () => new PublishProcessor<T>(bufferSize));
        }

        /// <summary>
        /// Shares a single connection to the upstream IFlowable for the duration of the handler
        /// function where the IFlowable provided can be subscribed to multiple times without
        /// causing new connections to the upstream source and multicast signals to all
        /// ISubscribers.
        /// </summary>
        /// <typeparam name="T">The source value type.</typeparam>
        /// <typeparam name="R">The result value type.</typeparam>
        /// <param name="source">The source IFlowable instance.</param>
        /// <param name="handler">The handler that receives an IFlowable that can
        /// be freely subscribed to multiple times without causing multiple subscriptions
        /// to the source IFlowable and should return an IPublisher representing the result
        /// whose signals will be relayed to downstream.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<R> Publish<T, R>(this IFlowable<T> source, Func<IFlowable<T>, IPublisher<R>> handler)
        {
            return Multicast(source, handler, PublishProcessorSupplier<T>.Instance);
        }

        /// <summary>
        /// Shares a single connection to the upstream IFlowable for the duration of the handler
        /// function where the IFlowable provided can be subscribed to multiple times without
        /// causing new connections to the upstream source and multicast signals to all
        /// ISubscribers.
        /// </summary>
        /// <typeparam name="T">The source value type.</typeparam>
        /// <typeparam name="R">The result value type.</typeparam>
        /// <param name="source">The source IFlowable instance.</param>
        /// <param name="handler">The handler that receives an IFlowable that can
        /// be freely subscribed to multiple times without causing multiple subscriptions
        /// to the source IFlowable and should return an IPublisher representing the result
        /// whose signals will be relayed to downstream.</param>
        /// <param name="bufferSize">The number of items to prefetch and buffer at a time.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<R> Publish<T, R>(this IFlowable<T> source, Func<IFlowable<T>, IPublisher<R>> handler, int bufferSize)
        {
            return Multicast(source, handler, () => new PublishProcessor<T>(bufferSize));
        }

        /// <summary>
        /// Multicasts the items of the upstream source IFlowable by sharing the same underlying
        /// connection to it and relaying/replaying all elements to current and future ISubscribers.
        /// </summary>
        /// <typeparam name="T">The input and output value type</typeparam>
        /// <param name="source">The source IFlowable instance.</param>
        /// <returns>The new IConnectableFlowable instance.</returns>
        public static IConnectableFlowable<T> Replay<T>(this IFlowable<T> source)
        {
            return Multicast(source, ReplayProcessorSupplier<T>.Instance);
        }


        /// <summary>
        /// Multicasts the items of the upstream source IFlowable by sharing the same underlying
        /// connection to it and relaying all elements for current subscribers but
        /// only the specified last number of elements to future ISubscribers.
        /// </summary>
        /// <typeparam name="T">The input and output value type</typeparam>
        /// <param name="source">The source IFlowable instance.</param>
        /// <param name="maxSize">The maximum number of items to cache for late ISubscribers.</param>
        /// <returns>The new IConnectableFlowable instance.</returns>
        public static IConnectableFlowable<T> Replay<T>(this IFlowable<T> source, int maxSize)
        {
            return Multicast(source, () => new ReplayProcessor<T>(maxSize));
        }

        /// <summary>
        /// Shares a single connection to the upstream IFlowable for the duration of the handler
        /// function where the IFlowable provided can be subscribed to multiple times without
        /// causing new connections to the upstream source and relays/replays all signals to all
        /// ISubscribers.
        /// </summary>
        /// <typeparam name="T">The source value type.</typeparam>
        /// <typeparam name="R">The result value type.</typeparam>
        /// <param name="source">The source IFlowable instance.</param>
        /// <param name="handler">The handler that receives an IFlowable that can
        /// be freely subscribed to multiple times without causing multiple subscriptions
        /// to the source IFlowable and should return an IPublisher representing the result
        /// whose signals will be relayed to downstream.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<R> Replay<T, R>(this IFlowable<T> source, Func<IFlowable<T>, IPublisher<R>> handler)
        {
            return Multicast(source, handler, ReplayProcessorSupplier<T>.Instance);
        }

        /// <summary>
        /// Shares a single connection to the upstream IFlowable for the duration of the handler
        /// function where the IFlowable provided can be subscribed to multiple times without
        /// causing new connections to the upstream source and relays all signals to early
        /// ISubscribers and the last given number of signals to late ISubscribers.
        /// </summary>
        /// <typeparam name="T">The source value type.</typeparam>
        /// <typeparam name="R">The result value type.</typeparam>
        /// <param name="source">The source IFlowable instance.</param>
        /// <param name="handler">The handler that receives an IFlowable that can
        /// be freely subscribed to multiple times without causing multiple subscriptions
        /// to the source IFlowable and should return an IPublisher representing the result
        /// whose signals will be relayed to downstream.</param>
        /// <param name="maxSize">The maximum number of items to replay to late subscribers.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<R> Replay<T, R>(this IFlowable<T> source, Func<IFlowable<T>, IPublisher<R>> handler, int maxSize)
        {
            return Multicast(source, handler, () => new ReplayProcessor<T>(maxSize));
        }

        /// <summary>
        /// Multicasts the items of the upstream source IFlowable by sharing the same underlying
        /// connection to it and routing events through the 
        /// IFlowableProcessor supplied via a function.
        /// </summary>
        /// <typeparam name="T">The input and output value type</typeparam>
        /// <param name="source">The source IFlowable instance.</param>
        /// <param name="processorSupplier">The function asked to return a fresh IFlowableProcessor
        /// instance to be used for sharing the connection to the source.</param>
        /// <returns>The new IConnectableFlowable instance.</returns>
        public static IConnectableFlowable<T> Multicast<T>(this IFlowable<T> source, Func<IFlowableProcessor<T>> processorSupplier)
        {
            return new ConnectableFlowableMulticast<T>(source, processorSupplier);
        }

        /// <summary>
        /// Shares a single connection to the upstream IFlowable 
        /// through an IFlowableProcessor supplied by the given function
        /// for the duration of the handler
        /// function where the IFlowable provided can be subscribed to multiple times without
        /// causing new connections to the upstream source.
        /// </summary>
        /// <typeparam name="T">The source value type.</typeparam>
        /// <typeparam name="R">The result value type.</typeparam>
        /// <param name="source">The source IFlowable instance.</param>
        /// <param name="handler">The handler that receives an IFlowable that can
        /// be freely subscribed to multiple times without causing multiple subscriptions
        /// to the source IFlowable and should return an IPublisher representing the result
        /// whose signals will be relayed to downstream.</param>
        /// <param name="processorSupplier">The function asked to return a fresh IFlowableProcessor
        /// instance to be used for sharing the connection to the source.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<R> Multicast<T, R>(this IFlowable<T> source, Func<IFlowable<T>, IPublisher<R>> handler, Func<IFlowableProcessor<T>> processorSupplier)
        {
            return new FlowableMulticast<T, R>(source, handler, processorSupplier);
        }

        /// <summary>
        /// Automatically call Connect() on the IConnectableFlowable when the number
        /// of subscribers reaches the specified amount.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The source IFlowable instance.</param>
        /// <param name="count">The number of ISubscribers to wait for before
        /// connecting to the source IConnectableFlowable.</param>
        /// <param name="onConnect">The action called with the connection's disposable
        /// that allows synchronous cancellation of the connection.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<T> AutoConnect<T>(this IConnectableFlowable<T> source, int count = 1, Action<IDisposable> onConnect = null)
        {
            if (count == 0)
            {
                source.Connect(onConnect);
                return source;
            }
            return new FlowableAutoConnect<T>(source, count, onConnect);
        }

        /// <summary>
        /// Connects to the IConnectableFlowable when the ISubscriber count passes the given
        /// count (default 1) and disconnects if all ISubscribers cancel their subscriptions,
        /// allowing to repeat this process as necessary.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The source IConnectableFlowable instance.</param>
        /// <param name="count">The number of ISubscribers required to connect the
        /// given IConnectableFlowable instance.</param>
        /// <returns>The new IFlowable instnace.</returns>
        public static IFlowable<T> RefCount<T>(this IConnectableFlowable<T> source, int count = 1)
        {
            return new FlowableRefCount<T>(source, count);
        }

        /// <summary>
        /// Disposes the ISubscription given to the IFlowableProcessor when the
        /// number of ISubscribers reaches zero.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The source IFlowableProcessor instance.</param>
        /// <returns>The new IFlowableProcessor instance</returns>
        public static IFlowableProcessor<T> RefCount<T>(this IFlowableProcessor<T> source)
        {
            if (source is FlowableProcessorRefCount<T>)
            {
                return source;
            }
            return new FlowableProcessorRefCount<T>(source);
        }

        /// <summary>
        /// Makes sure calls to OnNext(), OnError() and OnComplete() are properly
        /// serialized relative to each other and allows calling these methods
        /// concurrently.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The source IFlowableProcessor instance.</param>
        /// <returns>The new IFlowableProcessor instance.</returns>
        public static IFlowableProcessor<T> Serialize<T>(this IFlowableProcessor<T> source)
        {
            if (source is FlowableProcessorSerialize<T> s)
            {
                return s;
            }
            return new FlowableProcessorSerialize<T>(source);
        }

        // ********************************************************************************
        // State peeking methods
        // ********************************************************************************

        /// <summary>
        /// Calls the given action before the downstream receives an OnNext event.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The source IFlowable instance.</param>
        /// <param name="onNext">The action called with the current item before the downstream.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<T> DoOnNext<T>(this IFlowable<T> source, Action<T> onNext)
        {
            return FlowablePeek<T>.Create(source, onNext: onNext);
        }

        /// <summary>
        /// Calls the given action after the downstream received an OnNext event.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The source IFlowable instance.</param>
        /// <param name="onAfterNext">The action called with the current item after the downstream.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<T> DoAfterNext<T>(this IFlowable<T> source, Action<T> onAfterNext)
        {
            return FlowablePeek<T>.Create(source, onAfterNext: onAfterNext);
        }

        /// <summary>
        /// Calls the given action before the downstream receives an OnError event.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The source IFlowable instance.</param>
        /// <param name="onError">The action called with the error before the downstream receives an OnError.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<T> DoOnError<T>(this IFlowable<T> source, Action<Exception> onError)
        {
            return FlowablePeek<T>.Create(source, onError: onError);
        }

        /// <summary>
        /// Calls the given action before the downstream receives an OnComplete event.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The source IFlowable instance.</param>
        /// <param name="onComplete">The action called before the downstream receives an OnComplete.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<T> DoOnComplete<T>(this IFlowable<T> source, Action onComplete)
        {
            return FlowablePeek<T>.Create(source, onComplete: onComplete);
        }

        /// <summary>
        /// Calls the given action before the downstream receives an OnError or OnComplete event.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The source IFlowable instance.</param>
        /// <param name="onTerminated">The action called before the downstream receives an OnError or OnComplete event.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<T> DoOnTerminated<T>(this IFlowable<T> source, Action onTerminated)
        {
            return FlowablePeek<T>.Create(source, onTerminated: onTerminated);
        }

        /// <summary>
        /// Calls the given action after the downstream receives an OnError or OnComplete event.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The source IFlowable instance.</param>
        /// <param name="onAfterTerminated">The action called after the downstream receives an OnError or OnComplete event.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<T> DoAfterTerminated<T>(this IFlowable<T> source, Action onAfterTerminated)
        {
            return FlowablePeek<T>.Create(source, onAfterTerminated: onAfterTerminated);
        }

        /// <summary>
        /// Calls the given action exactly once per ISubscriber after the sequence terminates
        /// normally, with an error or the downstream cancels.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The source IFlowable instance.</param>
        /// <param name="onFinally">The action called when the upstream terminates or the downstream cancels.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<T> DoFinally<T>(this IFlowable<T> source, Action onFinally)
        {
            return new FlowableDoFinally<T>(source, onFinally);
        }

        /// <summary>
        /// Calls the given action before the downstream receives an OnSubscribe event.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The source IFlowable instance.</param>
        /// <param name="onSubscribe">The action called with the upstream ISubscription.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<T> DoOnSubscribe<T>(this IFlowable<T> source, Action<ISubscription> onSubscribe)
        {
            return FlowablePeek<T>.Create(source, onSubscribe: onSubscribe);
        }

        /// <summary>
        /// Calls the given action before the upstream gets requested.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The source IFlowable instance.</param>
        /// <param name="onRequest">The action called with the downstream request amount before
        /// it is relayed to the upstream.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<T> DoOnRequest<T>(this IFlowable<T> source, Action<long> onRequest)
        {
            return FlowablePeek<T>.Create(source, onRequest: onRequest);
        }

        /// <summary>
        /// Calls the given action before the upstream gets cancelled.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The source IFlowable instance.</param>
        /// <param name="onCancel">The action called before the cancellation is forwarded to the upstream.</param>
        /// <returns>The new IFlowable instance.</returns>
        public static IFlowable<T> DoOnCancel<T>(this IFlowable<T> source, Action onCancel)
        {
            return FlowablePeek<T>.Create(source, onCancel: onCancel);
        }

        // ********************************************************************************
        // Consumer methods
        // ********************************************************************************

        /// <summary>
        /// Consume the source IFlowable.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The source IFlowable instance.</param>
        /// <returns>The IDisposable that allows cancelling the sequence.</returns>
        public static IDisposable Subscribe<T>(this IFlowable<T> source)
        {
            return Subscribe(source, EmptyConsumer<T>.Instance, EmptyConsumer<Exception>.Instance, EmptyAction.Instance);
        }

        /// <summary>
        /// Consume the source IFlowable and call the given action
        /// with each item received.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The source IFlowable instance.</param>
        /// <param name="onNext">The Action called with each item received.</param>
        /// <returns>The IDisposable that allows cancelling the sequence.</returns>
        public static IDisposable Subscribe<T>(this IFlowable<T> source, Action<T> onNext)
        {
            return Subscribe(source, onNext, EmptyConsumer<Exception>.Instance, EmptyAction.Instance);
        }

        /// <summary>
        /// Consume the source IFlowable and call the given actions
        /// with each item received or the error.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The source IFlowable instance.</param>
        /// <param name="onNext">The Action called with each item received.</param>
        /// <param name="onError">The Action called with the error.</param>
        /// <returns>The IDisposable that allows cancelling the sequence.</returns>
        public static IDisposable Subscribe<T>(this IFlowable<T> source, Action<T> onNext, Action<Exception> onError)
        {
            return Subscribe(source, onNext, onError, EmptyAction.Instance);
        }

        /// <summary>
        /// Consume the source IFlowable and call the given actions
        /// with each item received, the error or the completion event.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The source IFlowable instance.</param>
        /// <param name="onNext">The Action called with each item received.</param>
        /// <param name="onError">The Action called with the error.</param>
        /// <param name="onComplete">The Action called when the upstream completes.</param>
        /// <returns>The IDisposable that allows cancelling the sequence.</returns>
        public static IDisposable Subscribe<T>(this IFlowable<T> source, Action<T> onNext, Action<Exception> onError, Action onComplete)
        {
            var s = new ActionSubscriber<T>(onNext, onError, onComplete);
            source.Subscribe(s);
            return s;
        }

        /// <summary>
        /// Subscribes the given IFlowableSubscriber (descendant) to the
        /// source IFlowable and return the same IFlowableSubscriber instance.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <typeparam name="S">The IFlowableSubscriber (subclass) type.</typeparam>
        /// <param name="source">The source IFlowable instance.</param>
        /// <param name="subscriber">The IFlowableSubsriber (subclass) instance.</param>
        /// <returns>The <paramref name="subscriber"/>.</returns>
        public static S SubscribeWith<T, S>(this IFlowable<T> source, S subscriber) where S : IFlowableSubscriber<T>
        {
            source.Subscribe(subscriber);
            return subscriber;
        }

        /// <summary>
        /// Returns a Task that returns the first element from the source
        /// IFlowable.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The source IFlowable instance.</param>
        /// <param name="cts">The cancellation token source to allow cancelling the sequence.</param>
        /// <returns>The new Task instance.</returns>
        public static Task<T> FirstTask<T>(this IFlowable<T> source, CancellationTokenSource cts)
        {
            var s = new TaskFirstSubscriber<T>(cts);
            source.Subscribe(s);
            return s.Task;
        }

        /// <summary>
        /// Returns a Task that returns the last element from the source
        /// IFlowable.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The source IFlowable instance.</param>
        /// <param name="cts">The cancellation token source to allow cancelling the sequence.</param>
        /// <returns>The new Task instance.</returns>
        public static Task<T> LastTask<T>(this IFlowable<T> source, CancellationTokenSource cts)
        {
            var s = new TaskLastSubscriber<T>(cts);
            source.Subscribe(s);
            return s.Task;
        }

        /// <summary>
        /// Returns a Task that succeeds or fails when the source
        /// IFlowable terminates.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The source IFlowable instance.</param>
        /// <param name="cts">The cancellation token source to allow cancelling the sequence.</param>
        /// <returns>The new Task instance.</returns>
        public static Task IgnoreElementsTask<T>(this IFlowable<T> source, CancellationTokenSource cts)
        {
            var s = new TaskIgnoreElementsSubscriber<T>(cts);
            source.Subscribe(s);
            return s.Task;
        }

        // ********************************************************************************
        // Blocking operators
        // ********************************************************************************

        /// <summary>
        /// Blocks until the source produces its first item or completes.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The source IFlowable instance</param>
        /// <param name="result">the first item</param>
        /// <returns>True if the soruce produced an item, false if the source was empty.</returns>
        public static bool BlockingFirst<T>(this IFlowable<T> source, out T result)
        {
            var s = new BlockingFirstSubscriber<T>();
            source.Subscribe(s);
            return s.BlockingGet(out result);
        }

        /// <summary>
        /// Blocks until the source produces its last item or completes.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The source IFlowable instance</param>
        /// <param name="result">the first item</param>
        /// <returns>True if the soruce produced an item, false if the source was empty.</returns>
        public static bool BlockingLast<T>(this IFlowable<T> source, out T result)
        {
            var s = new BlockingLastSubscriber<T>();
            source.Subscribe(s);
            return s.BlockingGet(out result);
        }

        /// <summary>
        /// Returns an IEnumerable that when enumerated, subscribes to
        /// the source and blocks for each item and terminal event
        /// before relaying it through the IEnumerator.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The source IFlowable instance.</param>
        /// <returns>The new IEnumerable instance.</returns>
        public static IEnumerable<T> BlockingEnumerable<T>(this IFlowable<T> source)
        {
            return BlockingEnumerable(source, BufferSize());
        }

        /// <summary>
        /// Returns an IEnumerable that when enumerated, subscribes to
        /// the source and blocks for each item and terminal event
        /// before relaying it through the IEnumerator.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The source IFlowable instance.</param>
        /// <param name="prefetch">The number of items to prefetch.</param>
        /// <returns>The new IEnumerable instance.</returns>
        public static IEnumerable<T> BlockingEnumerable<T>(this IFlowable<T> source, int prefetch)
        {
            var parent = new BlockingEnumeratorSubscriber<T>(prefetch);
            source.Subscribe(parent);

            while (parent.MoveNext())
            {
                yield return parent.Current;
            }
            yield break;
        }

        /// <summary>
        /// Blocks until the source terminates.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The source IFlowable instance.</param>
        public static void BlockingSubscribe<T>(this IFlowable<T> source)
        {
            BlockingSubscribe(source, EmptyConsumer<T>.Instance, EmptyConsumer<Exception>.Instance, EmptyAction.Instance);
        }

        /// <summary>
        /// Consumes the IFlowable source in a blocking
        /// fashion and calls the action on the current thread.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The source IFlowable instance.</param>
        /// <param name="onNext">The action to call with the items from the source.</param>
        public static void BlockingSubscribe<T>(this IFlowable<T> source, Action<T> onNext)
        {
            BlockingSubscribe(source, onNext, EmptyConsumer<Exception>.Instance, EmptyAction.Instance);
        }

        /// <summary>
        /// Consumes the IFlowable source in a blocking
        /// fashion and calls the actions on the current thread.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The source IFlowable instance.</param>
        /// <param name="onNext">The action to call with the items from the source.</param>
        /// <param name="onError">The action to call with the error from the source.</param>
        public static void BlockingSubscribe<T>(this IFlowable<T> source, Action<T> onNext, Action<Exception> onError)
        {
            BlockingSubscribe(source, onNext, onError, EmptyAction.Instance);
        }

        /// <summary>
        /// Consumes the IFlowable source in a blocking
        /// fashion and calls the actions on the current thread.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The source IFlowable instance.</param>
        /// <param name="onNext">The action to call with the items from the source.</param>
        /// <param name="onError">The action to call with the error from the source.</param>
        /// <param name="onComplete">The action to to call when the source terminates.</param>
        /// <param name="onConnect">The optional action called with the IDisposable representing the
        /// active connection that allows cancelling the flow.</param>
        public static void BlockingSubscribe<T>(this IFlowable<T> source, Action<T> onNext, Action<Exception> onError, Action onComplete, Action<IDisposable> onConnect = null)
        {
            BlockingSubscribe(source, onNext, onError, onComplete, BufferSize(), onConnect);
        }

        /// <summary>
        /// Consumes the IFlowable source in a blocking
        /// fashion and calls the actions on the current thread.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The source IFlowable instance.</param>
        /// <param name="onNext">The action to call with the items from the source.</param>
        /// <param name="onError">The action to call with the error from the source.</param>
        /// <param name="onComplete">The action to to call when the source terminates.</param>
        /// <param name="prefetch">The number of items to prefetch.</param>
        /// <param name="onConnect">The optional action called with the IDisposable representing the
        /// active connection that allows cancelling the flow.</param>
        public static void BlockingSubscribe<T>(this IFlowable<T> source, Action<T> onNext, Action<Exception> onError, Action onComplete, int prefetch, Action<IDisposable> onConnect = null)
        {
            var s = new BlockingLambdaSubscriber<T>(prefetch, onNext, onError, onComplete);
            onConnect?.Invoke(s);
            source.Subscribe(s);
            s.Run();
        }

        /// <summary>
        /// Consumes the IFlowable source and relays events to the
        /// given IFlowableSubscriber instance.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The source IFlowable instance.</param>
        /// <param name="subscriber">The IFlowableSubscriber that consumes the signals.</param>
        public static void BlockingSubscribe<T>(this IFlowable<T> source, IFlowableSubscriber<T> subscriber)
        {
            BlockingSubscribe(source, subscriber, BufferSize());
        }

        /// <summary>
        /// Consumes the IFlowable source and relays events to the
        /// given IFlowableSubscriber instance.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The source IFlowable instance.</param>
        /// <param name="subscriber">The IFlowableSubscriber that consumes the signals.</param>
        /// <param name="prefetch">The number of items to prefetch from the source.</param>
        public static void BlockingSubscribe<T>(this IFlowable<T> source, IFlowableSubscriber<T> subscriber, int prefetch)
        {
            var s = new BlockingSubscriber<T>(prefetch, subscriber);
            source.Subscribe(s);
            s.Run();
        }

        // ********************************************************************************
        // Test methods
        // ********************************************************************************

        /// <summary>
        /// Creates a TestSubscriber with the given initial settings
        /// and subscribes it to the source IFlowable.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The source IFlowable instance.</param>
        /// <param name="initialRequest">The initial request amount, non-negative, default long.MaxValue.</param>
        /// <param name="cancel">If true, the TestSubscriber will be cancelled before subscribing to the source.</param>
        /// <returns>The new TestSubscriber instance.</returns>
        public static TestSubscriber<T> Test<T>(this IFlowable<T> source, long initialRequest = long.MaxValue, bool cancel = false)
        {
            var ts = new TestSubscriber<T>(initialRequest);
            if (cancel)
            {
                ts.Cancel();
            }
            source.Subscribe(ts);
            return ts;
        }
    }
}
