using Reactive4.NET.operators;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Reactive.Streams;
using Reactive4.NET.subscribers;
using System.Threading;

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
        /// <returns></returns>
        public static int BufferSize()
        {
            return 128;
        }

        // ********************************************************************************
        // Interop methods
        // ********************************************************************************

        public static IFlowable<T> ToFlowable<T>(this IPublisher<T> publisher)
        {
            if (publisher is IFlowable<T> f)
            {
                return f;
            }
            return new FlowableFromPublisher<T>(publisher);
        }

        public static IFlowable<T> FromPublisher<T>(IPublisher<T> publisher)
        {
            return publisher.ToFlowable();
        }

        public static IFlowable<T> ToFlowable<T>(this IObservable<T> source, BackpressureStrategy strategy)
        {
            return new FlowableFromObservable<T>(source, strategy);
        }

        public static IFlowable<T> FromObservable<T>(IObservable<T> source, BackpressureStrategy strategy)
        {
            return source.ToFlowable(strategy);
        }

        public static IFlowable<T> ToFlowable<T>(this Task<T> task)
        {
            return new FlowableFromTask<T>(task);
        }

        public static IFlowable<T> FromTask<T>(Task<T> task)
        {
            return task.ToFlowable();
        }

        public static IFlowable<object> ToFlowable(this Task task)
        {
            return new FlowableFromTaskVoid(task);
        }

        public static IFlowable<object> FromTask(this Task task)
        {
            return task.ToFlowable();
        }

        public static IObservable<T> ToObservable<T>(this IFlowable<T> source)
        {
            return new FlowableToObservable<T>(source);
        }

        // ********************************************************************************
        // Factory methods
        // ********************************************************************************

        public static IFlowable<T> Just<T>(T item)
        {
            return new FlowableJust<T>(item);
        }

        public static IFlowable<T> RepeatItem<T>(T item)
        {
            return new FlowableRepeatItem<T>(item);
        }

        public static IFlowable<T> Error<T>(Exception exception)
        {
            return new FlowableError<T>(exception);
        }

        public static IFlowable<T> Error<T>(Func<Exception> errorSupplier)
        {
            return new FlowableErrorSupplier<T>(errorSupplier);
        }

        public static IFlowable<T> Empty<T>()
        {
            return FlowableEmpty<T>.Instance;
        }

        public static IFlowable<T> Never<T>()
        {
            return FlowableNever<T>.Instance;
        }

        public static IFlowable<T> FromFunction<T>(Func<T> function)
        {
            return new FlowableFromFunction<T>(function);
        }

        public static IFlowable<T> RepeatFunction<T>(Func<T> function)
        {
            return new FlowableRepeatFunction<T>(function);
        }

        public static IFlowable<T> Create<T>(Action<IFlowableEmitter<T>> emitter, BackpressureStrategy strategy)
        {
            return new FlowableCreate<T>(emitter, strategy);
        }

        public static IFlowable<T> Generate<T>(Action<IGeneratorEmitter<T>> emitter)
        {
            return Generate<T, object>(() => null, (s, e) => { emitter(e); return null; }, s => { });
        }

        public static IFlowable<T> Generate<T, S>(Func<S> stateFactory, Action<S, IGeneratorEmitter<T>> emitter)
        {
            return Generate<T, S>(stateFactory, (s, e) => { emitter(s, e); return s; }, s => { });
        }

        public static IFlowable<T> Generate<T, S>(Func<S> stateFactory, Action<S, IGeneratorEmitter<T>> emitter, Action<S> stateCleanup, bool eager = false)
        {
            return Generate<T, S>(stateFactory, (s, e) => { emitter(s, e); return s; }, stateCleanup, eager);
        }

        public static IFlowable<T> Generate<T, S>(Func<S> stateFactory, Func<S, IGeneratorEmitter<T>, S> emitter)
        {
            return Generate<T, S>(stateFactory, emitter, s => { });
        }

        public static IFlowable<T> Generate<T, S>(Func<S> stateFactory, Func<S, IGeneratorEmitter<T>, S> emitter, Action<S> stateCleanup, bool eager = false)
        {
            return new FlowableGenerate<T, S>(stateFactory, emitter, stateCleanup, eager);
        }

        public static IFlowable<T> FromArray<T>(params T[] items)
        {
            return new FlowableArray<T>(items);
        }

        public static IFlowable<T> FromEnumerable<T>(IEnumerable<T> items)
        {
            return new FlowableEnumerable<T>(items);
        }

        public static IFlowable<int> Range(int start, int count)
        {
            return new FlowableRange(start, start + count);
        }

        public static IFlowable<T> Defer<T>(Func<IPublisher<T>> supplier)
        {
            return new FlowableDefer<T>(supplier);
        }

        public static IFlowable<long> Timer(TimeSpan delay)
        {
            return Timer(delay, Executors.Computation);
        }

        public static IFlowable<long> Timer(TimeSpan delay, IExecutorService executor)
        {
            return new FlowableTimer(delay, executor);
        }

        public static IFlowable<long> Interval(TimeSpan period)
        {
            return Interval(period, period, Executors.Computation);
        }

        public static IFlowable<long> Interval(TimeSpan initialDelay, TimeSpan period)
        {
            return Interval(initialDelay, period, Executors.Computation);
        }

        public static IFlowable<long> Interval(TimeSpan period, IExecutorService executor)
        {
            return Interval(period, period, executor);
        }

        public static IFlowable<long> Interval(TimeSpan initialDelay, TimeSpan period, IExecutorService executor)
        {
            return new FlowableInterval(initialDelay, period, executor);
        }

        public static IFlowable<T> Using<T, D>(Func<D> resourceFactory, Func<D, IPublisher<T>> resourceMapper, Action<D> resourceCleanup = null, bool eager = true)
        {
            return new FlowableUsing<T, D>(resourceFactory, resourceMapper, resourceCleanup, eager);
        }

        // ********************************************************************************
        // Multi-source factory methods
        // ********************************************************************************

        public static IFlowable<T> Amb<T>(params IPublisher<T>[] sources) {
            // TODO implement
            throw new NotImplementedException();
        }

        public static IFlowable<T> Amb<T>(IEnumerable<IPublisher<T>> sources)
        {
            // TODO implement
            throw new NotImplementedException();
        }

        public static IFlowable<T> Concat<T>(params IPublisher<T>[] sources)
        {
            return new FlowableConcatArray<T>(sources);
        }

        public static IFlowable<T> Concat<T>(IEnumerable<IPublisher<T>> sources)
        {
            return new FlowableConcatEnumerable<T>(sources);
        }

        public static IFlowable<T> Concat<T>(this IPublisher<IPublisher<T>> sources)
        {
            return Concat(sources, BufferSize());
        }

        public static IFlowable<T> Concat<T>(this IPublisher<IPublisher<T>> sources, int prefetch)
        {
            return new FlowableConcatMapPublisher<IPublisher<T>, T>(sources, v => v, prefetch);
        }

        public static IFlowable<T> ConcatEager<T>(params IPublisher<T>[] sources)
        {
            return FromArray(sources).ConcatMapEager(v => v);
        }

        public static IFlowable<T> ConcatEager<T>(IEnumerable<IPublisher<T>> sources)
        {
            return ConcatEager(sources, BufferSize(), BufferSize());
        }

        public static IFlowable<T> ConcatEager<T>(IEnumerable<IPublisher<T>> sources, int maxConcurrency)
        {
            return ConcatEager(sources, maxConcurrency, BufferSize());
        }

        public static IFlowable<T> ConcatEager<T>(IEnumerable<IPublisher<T>> sources, int maxConcurrency, int prefetch)
        {
            return FromEnumerable(sources).ConcatMapEager(v => v, maxConcurrency, prefetch);
        }

        public static IFlowable<T> ConcatEager<T>(this IPublisher<IPublisher<T>> sources)
        {
            return ConcatEager(sources, BufferSize(), BufferSize());
        }

        public static IFlowable<T> ConcatEager<T>(this IPublisher<IPublisher<T>> sources, int maxConcurrency)
        {
            return ConcatEager(sources, maxConcurrency, BufferSize());
        }

        public static IFlowable<T> ConcatEager<T>(this IPublisher<IPublisher<T>> sources, int maxConcurrency, int prefetch)
        {
            // TODO implement
            throw new NotImplementedException();
        }

        public static IFlowable<T> Merge<T>(params IPublisher<T>[] sources)
        {
            return FromArray(sources).FlatMap(v => v);
        }

        public static IFlowable<T> Merge<T>(IEnumerable<IPublisher<T>> sources)
        {
            return FromEnumerable(sources).FlatMap(v => v);
        }

        public static IFlowable<T> Merge<T>(IEnumerable<IPublisher<T>> sources, int maxConcurrency)
        {
            return FromEnumerable(sources).FlatMap(v => v, maxConcurrency);
        }

        public static IFlowable<T> Merge<T>(IEnumerable<IPublisher<T>> sources, int maxConcurrency, int prefetch)
        {
            return FromEnumerable(sources).FlatMap(v => v, maxConcurrency, prefetch);
        }

        public static IFlowable<T> Merge<T>(this IPublisher<IPublisher<T>> sources)
        {
            return Merge(sources, BufferSize(), BufferSize());
        }

        public static IFlowable<T> Merge<T>(this IPublisher<IPublisher<T>> sources, int maxConcurrency)
        {
            return Merge(sources, maxConcurrency, BufferSize());
        }

        public static IFlowable<T> Merge<T>(this IPublisher<IPublisher<T>> sources, int maxConcurrency, int bufferSize)
        {
            return new FlowableFlatMapPublisher<IPublisher<T>, T>(sources, v => v, maxConcurrency, bufferSize);
        }

        public static IFlowable<R> CombineLatest<T, R>(Func<T[], R> combiner, params IPublisher<T>[] sources)
        {
            // TODO implement
            throw new NotImplementedException();
        }

        public static IFlowable<R> CombineLatest<T, R>(IEnumerable<IPublisher<T>> sources, Func<T[], R> combiner)
        {
            return CombineLatest(sources, combiner, BufferSize());
        }

        public static IFlowable<R> CombineLatest<T, R>(IEnumerable<IPublisher<T>> sources, Func<T[], R> combiner, int prefetch)
        {
            // TODO implement
            throw new NotImplementedException();
        }

        public static IFlowable<R> CombineLatest<T1, T2, R>(IPublisher<T1> source1, IPublisher<T2> source2, Func<T1, T2, R> combiner)
        {
            // TODO implement
            throw new NotImplementedException();
        }

        public static IFlowable<R> CombineLatest<T1, T2, T3, R>(IPublisher<T1> source1, IPublisher<T2> source2,
            IPublisher<T3> source3, Func<T1, T2, T3, R> combiner)
        {
            // TODO implement
            throw new NotImplementedException();
        }

        public static IFlowable<R> CombineLatest<T1, T2, T3, T4, R>(IPublisher<T1> source1, IPublisher<T2> source2,
            IPublisher<T3> source3, IPublisher<T4> source4, Func<T1, T2, T3, T4, R> combiner)
        {
            // TODO implement
            throw new NotImplementedException();
        }

        public static IFlowable<R> Zip<T, R>(Func<T[], R> zipper, params IPublisher<T>[] sources)
        {
            // TODO implement
            throw new NotImplementedException();
        }

        public static IFlowable<R> Zip<T, R>(IEnumerable<IPublisher<T>> sources, Func<T[], R> zipper)
        {
            // TODO implement
            throw new NotImplementedException();
        }

        public static IFlowable<R> Zip<T1, T2, R>(IPublisher<T1> source1, IPublisher<T2> source2, Func<T1, T2, R> zipper)
        {
            // TODO implement
            throw new NotImplementedException();
        }

        public static IFlowable<R> Zip<T1, T2, T3, R>(IPublisher<T1> source1, IPublisher<T2> source2, 
            IPublisher<T3> source3, Func<T1, T2, T3, R> zipper)
        {
            // TODO implement
            throw new NotImplementedException();
        }

        public static IFlowable<R> Zip<T1, T2, T3, T4, R>(IPublisher<T1> source1, IPublisher<T2> source2,
            IPublisher<T3> source3, IPublisher<T4> source4, Func<T1, T2, T3, T4, R> zipper)
        {
            // TODO implement
            throw new NotImplementedException();
        }

        public static IFlowable<T> SwitchOnNext<T>(IPublisher<IPublisher<T>> sources)
        {
            // TODO implement
            throw new NotImplementedException();
        }

        // ********************************************************************************
        // Instance operators
        // ********************************************************************************

        public static IParallelFlowable<T> Parallel<T>(this IFlowable<T> source)
        {
            return Parallel(source, Environment.ProcessorCount, BufferSize());
        }

        public static IParallelFlowable<T> Parallel<T>(this IFlowable<T> source, int parallelism)
        {
            return Parallel(source, parallelism, BufferSize());
        }

        public static IParallelFlowable<T> Parallel<T>(this IFlowable<T> source, int parallelism, int bufferSize)
        {
            // TODO implement
            throw new NotImplementedException();
        }

        public static IFlowable<R> Map<T, R>(this IFlowable<T> source, Func<T, R> mapper)
        {
            return new FlowableMap<T, R>(source, mapper);
        }

        public static IFlowable<R> MapAsync<T, R>(this IFlowable<T> source, Func<T, IPublisher<R>> mapper)
        {
            // TODO implement
            throw new NotImplementedException();
        }

        public static IFlowable<R> MapAsync<T, U, R>(this IFlowable<T> source, Func<T, IPublisher<U>> mapper, Func<T, U, R> resultMapper)
        {
            // TODO implement
            throw new NotImplementedException();
        }

        public static IFlowable<T> Filter<T>(this IFlowable<T> source, Func<T, bool> predicate)
        {
            return new FlowableFilter<T>(source, predicate);
        }

        public static IFlowable<T> FilterAsync<T>(this IFlowable<T> source, Func<T, IPublisher<bool>> predicate)
        {
            // TODO implement
            throw new NotImplementedException();
        }

        public static IFlowable<T> Take<T>(this IFlowable<T> source, long n, bool limitRequest = false)
        {
            return new FlowableTake<T>(source, n, limitRequest);
        }

        public static IFlowable<T> Skip<T>(this IFlowable<T> source, long n)
        {
            return new FlowableSkip<T>(source, n);
        }

        public static IFlowable<T> TakeLast<T>(this IFlowable<T> source, long n)
        {
            if (n <= 0)
            {
                return Empty<T>();
            }
            if (n == 1L)
            {
                return new FlowableTakeLastOne<T>(source);
            }
            // TODO implement
            throw new NotImplementedException();
        }

        public static IFlowable<T> SkipLast<T>(this IFlowable<T> source, long n)
        {
            if (n <= 0)
            {
                return source;
            }
            // TODO implement
            throw new NotImplementedException();
        }

        public static IFlowable<C> Collect<T, C>(this IFlowable<T> source, Func<C> collectionSupplier, Action<C, T> collector)
        {
            return new FlowableCollect<T, C>(source, collectionSupplier, collector);
        }

        public static IFlowable<T> Reduce<T>(this IFlowable<T> source, Func<T, T, T> reducer)
        {
            return new FlowableReducePlain<T>(source, reducer);
        }

        public static IFlowable<R> Reduce<T, R>(this IFlowable<T> source, Func<R> initialSupplier, Func<R, T, R> reducer)
        {
            return new FlowableReduce<T, R>(source, initialSupplier, reducer);
        }

        public static IFlowable<IList<T>> ToList<T>(this IFlowable<T> source, int capacityHint = 10)
        {
            return Collect(source, () => new List<T>(capacityHint), (a, b) => a.Add(b));
        }

        public static IFlowable<int> SumInt(this IFlowable<int> source)
        {
            return Reduce(source, (a, b) => a + b);
        }

        public static IFlowable<long> SumLong(this IFlowable<long> source)
        {
            return Reduce(source, (a, b) => a + b);
        }

        public static IFlowable<int> MaxInt(this IFlowable<int> source)
        {
            return Reduce(source, (a, b) => Math.Max(a, b));
        }

        public static IFlowable<T> Max<T>(this IFlowable<T> source, IComparer<T> comparer)
        {
            return Reduce(source, (a, b) => comparer.Compare(a, b) < 0 ? b : a);
        }

        public static IFlowable<long> MaxLong(this IFlowable<long> source)
        {
            return Reduce(source, (a, b) => Math.Max(a, b));
        }

        public static IFlowable<int> MinInt(this IFlowable<int> source)
        {
            return Reduce(source, (a, b) => Math.Min(a, b));
        }

        public static IFlowable<long> MinLong(this IFlowable<long> source)
        {
            return Reduce(source, (a, b) => Math.Min(a, b));
        }

        public static IFlowable<T> Min<T>(this IFlowable<T> source, IComparer<T> comparer)
        {
            return Reduce(source, (a, b) => comparer.Compare(a, b) < 0 ? a : b);
        }

        public static IFlowable<T> IgnoreElements<T>(this IFlowable<T> source)
        {
            return new FlowableIgnoreElements<T>(source);
        }

        public static IFlowable<R> FlatMap<T, R>(this IFlowable<T> source, Func<T, IPublisher<R>> mapper)
        {
            return FlatMap(source, mapper, BufferSize(), BufferSize());
        }

        public static IFlowable<R> FlatMap<T, R>(this IFlowable<T> source, Func<T, IPublisher<R>> mapper, int maxConcurrency)
        {
            return FlatMap(source, mapper, maxConcurrency, BufferSize());
        }

        public static IFlowable<R> FlatMap<T, R>(this IFlowable<T> source, Func<T, IPublisher<R>> mapper, int maxConcurrency, int bufferSize)
        {
            return new FlowableFlatMap<T, R>(source, mapper, maxConcurrency, bufferSize);
        }

        public static IFlowable<T> SubscribeOn<T>(this IFlowable<T> source, IExecutorService executor, bool requestOn = true)
        {
            return new FlowableSubscribeOn<T>(source, executor, requestOn);
        }

        public static IFlowable<T> ObserveOn<T>(this IFlowable<T> source, IExecutorService executor)
        {
            return ObserveOn(source, executor, BufferSize());
        }

        public static IFlowable<T> ObserveOn<T>(this IFlowable<T> source, IExecutorService executor, int bufferSize)
        {
            return new FlowableObserveOn<T>(source, executor, bufferSize);
        }

        public static IFlowable<T> RebatchRequests<T>(this IFlowable<T> source, int batchSize)
        {
            return ObserveOn(source, Executors.Immediate, batchSize);
        }

        public static IFlowable<T> Delay<T>(this IFlowable<T> source, TimeSpan delay)
        {
            return Delay(source, delay, Executors.Computation);
        }

        public static IFlowable<T> Delay<T>(this IFlowable<T> source, TimeSpan delay, IExecutorService executor)
        {
            return new FlowableDelay<T>(source, delay, executor);
        }

        public static IFlowable<T> DelaySubscription<T, U>(this IFlowable<T> source, IPublisher<U> other)
        {
            return new FlowableDelaySubscription<T, U>(source, other);
        }

        public static IFlowable<T> DelaySubscription<T>(this IFlowable<T> source, TimeSpan delay)
        {
            return DelaySubscription(source, Timer(delay, Executors.Computation));
        }

        public static IFlowable<T> DelaySubscription<T, U>(this IFlowable<T> source, TimeSpan delay, IExecutorService executor)
        {
            return DelaySubscription(source, Timer(delay, executor));
        }

        public static IFlowable<R> ConcatMap<T, R>(this IFlowable<T> source, Func<T, IPublisher<R>> mapper)
        {
            return ConcatMap(source, mapper, 2);
        }

        public static IFlowable<R> ConcatMap<T, R>(this IFlowable<T> source, Func<T, IPublisher<R>> mapper, int prefetch)
        {
            return new FlowableConcatMap<T, R>(source, mapper, prefetch);
        }

        public static IFlowable<R> ConcatMapEager<T, R>(this IFlowable<T> source, Func<T, IPublisher<R>> mapper)
        {
            return ConcatMapEager(source, mapper, BufferSize(), BufferSize());
        }

        public static IFlowable<R> ConcatMapEager<T, R>(this IFlowable<T> source, Func<T, IPublisher<R>> mapper, int maxConcurrency)
        {
            // TODO implement
            throw new NotImplementedException();
        }

        public static IFlowable<R> ConcatMapEager<T, R>(this IFlowable<T> source, Func<T, IPublisher<R>> mapper, int maxConcurrency, int prefetch)
        {
            // TODO implement
            throw new NotImplementedException();
        }

        public static IFlowable<T> Hide<T>(this IFlowable<T> source)
        {
            return new FlowableHide<T>(source);
        }

        public static IFlowable<T> Distinct<T>(this IFlowable<T> source)
        {
            return Distinct(source, EqualityComparer<T>.Default);
        }

        public static IFlowable<T> Distinct<T>(this IFlowable<T> source, IEqualityComparer<T> comparer)
        {
            return new FlowableDistinct<T>(source, comparer);
        }

        public static IFlowable<T> DistinctUntilChanged<T>(this IFlowable<T> source)
        {
            return DistinctUntilChanged(source, EqualityComparer<T>.Default);
        }

        public static IFlowable<T> DistinctUntilChanged<T>(this IFlowable<T> source, IEqualityComparer<T> comparer)
        {
            return new FlowableDistinctUntilChanged<T>(source, comparer);
        }

        public static IFlowable<T> TakeUntil<T, U>(this IFlowable<T> source, IPublisher<U> other)
        {
            return new FlowableTakeUntil<T, U>(source, other);
        }

        public static IFlowable<T> SkipUntil<T, U>(this IFlowable<T> source, IPublisher<U> other)
        {
            return new FlowableSkipUntil<T, U>(source, other);
        }

        public static IFlowable<R> Lift<T, R>(this IFlowable<T> source, Func<IFlowableSubscriber<R>, IFlowableSubscriber<T>> lifter)
        {
            return new FlowableLift<T, R>(source, lifter);
        }

        public static IFlowable<R> Compose<T, R>(this IFlowable<T> source, Func<IFlowable<T>, IPublisher<R>> composer)
        {
            return composer(source).ToFlowable();
        }

        public static R To<T, R>(this IFlowable<T> source, Func<IFlowable<T>, R> converter)
        {
            return converter(source);
        }

        public static IFlowable<R> DeferComose<T, R>(this IFlowable<T> source, Func<IFlowable<T>, IPublisher<R>> composer)
        {
            return Defer(() => composer(source));
        }

        public static IFlowable<R> SwitchMap<T, R>(this IFlowable<T> source, Func<T, IPublisher<R>> mapper)
        {
            // TODO implement
            throw new NotImplementedException();
        }

        public static IFlowable<T> DefaultIfEmpty<T>(this IFlowable<T> source, T defaultItem)
        {
            // TODO implement
            throw new NotImplementedException();
        }

        public static IFlowable<T> SwitchIfEmpty<T>(this IFlowable<T> source, IPublisher<T> fallback)
        {
            // TODO implement
            throw new NotImplementedException();
        }

        public static IFlowable<T> SwitchIfEmpty<T>(this IFlowable<T> source, params IPublisher<T>[] fallbacks)
        {
            // TODO implement
            throw new NotImplementedException();
        }

        public static IFlowable<T> SwitchIfEmpty<T>(this IFlowable<T> source, IEnumerable<IPublisher<T>> fallbacks)
        {
            // TODO implement
            throw new NotImplementedException();
        }

        public static IFlowable<T> Repeat<T>(this IFlowable<T> source, long times = long.MaxValue)
        {
            // TODO implement
            throw new NotImplementedException();
        }

        public static IFlowable<T> Repeat<T>(this IFlowable<T> source, Func<bool> stop, long times = long.MaxValue)
        {
            // TODO implement
            throw new NotImplementedException();
        }

        public static IFlowable<T> RepeatWhen<T, U>(this IFlowable<T> source, Func<IFlowable<object>, IPublisher<U>> handler)
        {
            // TODO implement
            throw new NotImplementedException();
        }

        public static IFlowable<T> Retry<T>(this IFlowable<T> source, long times = long.MaxValue)
        {
            // TODO implement
            throw new NotImplementedException();
        }

        public static IFlowable<T> Retry<T>(this IFlowable<T> source, Func<Exception, bool> predicate, long times = long.MaxValue)
        {
            // TODO implement
            throw new NotImplementedException();
        }

        public static IFlowable<T> RetryWhen<T, U>(this IFlowable<T> source, Func<IFlowable<Exception>, IPublisher<U>> handler)
        {
            // TODO implement
            throw new NotImplementedException();
        }

        public static IFlowable<T> OnErrorReturn<T>(this IFlowable<T> source, T fallbackItem)
        {
            // TODO implement
            throw new NotImplementedException();
        }

        public static IFlowable<T> OnErrorComplete<T>(this IFlowable<T> source)
        {
            // TODO implement
            throw new NotImplementedException();
        }

        public static IFlowable<T> OnErrorResumeNext<T>(this IFlowable<T> source, IPublisher<T> fallback)
        {
            // TODO implement
            throw new NotImplementedException();
        }

        public static IFlowable<T> OnErrorResumeNext<T>(this IFlowable<T> source, Func<Exception, IPublisher<T>> handler)
        {
            // TODO implement
            throw new NotImplementedException();
        }

        public static IFlowable<T> Timeout<T>(this IFlowable<T> source, TimeSpan itemTimeout, IPublisher<T> fallback = null)
        {
            return Timeout<T>(source, itemTimeout, Executors.Computation, fallback);
        }

        public static IFlowable<T> Timeout<T>(this IFlowable<T> source, TimeSpan itemTimeout, IExecutorService executor, IPublisher<T> fallback = null)
        {
            // TODO implement
            throw new NotImplementedException();
        }

        public static IFlowable<T> Timeout<T>(this IFlowable<T> source, TimeSpan firstTimeout, TimeSpan itemTimeout, IPublisher<T> fallback = null)
        {
            return Timeout<T>(source, firstTimeout, itemTimeout, Executors.Computation, fallback);
        }

        public static IFlowable<T> Timeout<T>(this IFlowable<T> source, TimeSpan firstTimeout, TimeSpan itemTimeout, IExecutorService executor, IPublisher<T> fallback = null)
        {
            // TODO implement
            throw new NotImplementedException();
        }

        public static IFlowable<T> OnBackpressureError<T>(this IFlowable<T> source)
        {
            // TODO implement
            throw new NotImplementedException();
        }

        public static IFlowable<T> OnBackpressureDrop<T>(this IFlowable<T> source, Action<T> onDrop = null)
        {
            // TODO implement
            throw new NotImplementedException();
        }

        public static IFlowable<T> OnBackpressureLatest<T>(this IFlowable<T> source)
        {
            // TODO implement
            throw new NotImplementedException();
        }

        public static IFlowable<T> OnBackpressureBuffer<T>(this IFlowable<T> source)
        {
            return OnBackpressureBuffer(source, BufferSize(), BufferStrategy.ALL, null);
        }

        public static IFlowable<T> OnBackpressureBuffer<T>(this IFlowable<T> source, int capacityHint, BufferStrategy strategy = BufferStrategy.ALL, Action<T> onDrop = null)
        {
            if (strategy == BufferStrategy.ALL)
            {
                return new FlowableOnBackpressureBufferAll<T>(source, capacityHint);
            }
            // TODO implement
            throw new NotImplementedException();
        }

        public static IFlowable<IGroupedFlowable<K, T>> GroupBy<T, K>(this IFlowable<T> source, Func<T, K> keyMapper)
        {
            return GroupBy<T, K, T>(source, keyMapper, v => v);
        }

        public static IFlowable<IGroupedFlowable<K, V>> GroupBy<T, K, V>(this IFlowable<T> source, Func<T, K> keyMapper, Func<T, V> valueMapper)
        {
            // TODO implement
            throw new NotImplementedException();
        }

        public static IFlowable<R> WithLatestFrom<T, U, R>(this IFlowable<T> source, IPublisher<U> other, Func<T, U, R> combiner)
        {
            // TODO implement
            throw new NotImplementedException();
        }

        public static IFlowable<R> WithLatestFrom<T, R>(this IFlowable<T> source, Func<T[], R> combiner, params IPublisher<T>[] others)
        {
            // TODO implement
            throw new NotImplementedException();
        }

        public static IFlowable<R> WithLatestFrom<T, R>(this IFlowable<T> source, Func<T[], R> combiner, IEnumerable<IPublisher<T>> others)
        {
            // TODO implement
            throw new NotImplementedException();
        }

        public static IFlowable<T> Sample<T>(this IFlowable<T> source, TimeSpan period)
        {
            return Sample(source, period, Executors.Computation);
        }

        public static IFlowable<T> Sample<T>(this IFlowable<T> source, TimeSpan period, IExecutorService executor)
        {
            // TODO implement
            throw new NotImplementedException();
        }

        public static IFlowable<T> Sample<T, U>(this IFlowable<T> source, IPublisher<U> sampler)
        {
            // TODO implement
            throw new NotImplementedException();
        }

        public static IFlowable<T> Debounce<T>(this IFlowable<T> source, TimeSpan delay)
        {
            // TODO implement
            throw new NotImplementedException();
        }

        public static IFlowable<T> ThrottleFirst<T>(this IFlowable<T> source, TimeSpan delay)
        {
            // TODO implement
            throw new NotImplementedException();
        }

        public static IFlowable<T> ThrottleLast<T>(this IFlowable<T> source, TimeSpan delay)
        {
            // TODO implement
            throw new NotImplementedException();
        }

        public static IFlowable<T> ThrottleWithTimeout<T>(this IFlowable<T> source, TimeSpan delay)
        {
            // TODO implement
            throw new NotImplementedException();
        }

        public static IFlowable<IList<T>> Buffer<T>(this IFlowable<T> source, int size)
        {
            return Buffer(source, size, size, () => new List<T>());
        }

        public static IFlowable<C> Buffer<T, C>(this IFlowable<T> source, int size, Func<C> collectionSupplier) where C : ICollection<T>
        {
            return Buffer(source, size, size, collectionSupplier);
        }

        public static IFlowable<IList<T>> Buffer<T>(this IFlowable<T> source, int size, int skip)
        {
            return Buffer(source, size, skip, () => new List<T>());
        }

        public static IFlowable<C> Buffer<T, C>(this IFlowable<T> source, int size, int skip, Func<C> collectionSupplier) where C : ICollection<T>
        {
            // TODO implement
            throw new NotImplementedException();
        }

        public static IFlowable<IList<T>> Buffer<T, U>(this IFlowable<T> source, IPublisher<U> boundary)
        {
            return Buffer(source, boundary, () => new List<T>());
        }

        public static IFlowable<C> Buffer<T, U, C>(this IFlowable<T> source, IPublisher<U> boundary, Func<C> collectionSupplier) where C : ICollection<T>
        {
            // TODO implement
            throw new NotImplementedException();
        }

        public static IFlowable<IFlowable<T>> Window<T>(this IFlowable<T> source, int size)
        {
            return Window(source, size, size);
        }

        public static IFlowable<IFlowable<T>> Window<T>(this IFlowable<T> source, int size, int skip)
        {
            // TODO implement
            throw new NotImplementedException();
        }

        public static IFlowable<IFlowable<T>> Window<T, U>(this IFlowable<T> source, IPublisher<U> boundary)
        {
            // TODO implement
            throw new NotImplementedException();
        }

        public static IFlowable<T> Scan<T, R>(this IFlowable<T> source, Func<T, T, T> scanner)
        {
            // TODO implement
            throw new NotImplementedException();
        }

        public static IFlowable<R> Scan<T, R>(this IFlowable<T> source, Func<R> initialSupplier, Func<R, T, R> scanner)
        {
            // TODO implement
            throw new NotImplementedException();
        }

        public static IFlowable<T> AmbWith<T>(this IFlowable<T> source, IPublisher<T> other)
        {
            return Amb(source, other);
        }

        public static IFlowable<T> ConcatWith<T>(this IFlowable<T> source, IPublisher<T> other)
        {
            return Concat(source, other);
        }

        public static IFlowable<T> MergeWith<T>(this IFlowable<T> source, IPublisher<T> other)
        {
            return Merge(source, other);
        }

        public static IFlowable<R> ZipWith<T, U, R>(this IFlowable<T> source, IPublisher<U> other, Func<T, U, R> zipper)
        {
            return Zip(source, other, zipper);
        }

        // ********************************************************************************
        // IConnectableFlowable related
        // ********************************************************************************

        public static IConnectableFlowable<T> Publish<T>(this IFlowable<T> source)
        {
            return Publish(source, BufferSize());
        }

        public static IConnectableFlowable<T> Publish<T>(this IFlowable<T> source, int bufferSize)
        {
            // TODO implement
            throw new NotImplementedException();
        }

        public static IFlowable<R> Publish<T, R>(this IFlowable<T> source, Func<IFlowable<T>, IPublisher<R>> handler)
        {
            return Publish(source, handler, BufferSize());
        }

        public static IFlowable<R> Publish<T, R>(this IFlowable<T> source, Func<IFlowable<T>, IPublisher<R>> handler, int bufferSize)
        {
            // TODO implement
            throw new NotImplementedException();
        }

        public static IConnectableFlowable<T> Replay<T>(this IFlowable<T> source)
        {
            // TODO implement
            throw new NotImplementedException();
        }

        public static IConnectableFlowable<R> Replay<T, R>(this IFlowable<T> source, Func<IFlowable<T>, IPublisher<R>> handler)
        {
            // TODO implement
            throw new NotImplementedException();
        }

        public static IFlowable<T> AutoConnect<T>(this IConnectableFlowable<T> source, int count = 1, Action<IDisposable> onConnect = null)
        {
            if (count == 0)
            {
                source.Connect(onConnect);
                return source;
            }
            return new FlowableAutoConnect<T>(source, count, onConnect);
        }

        public static IFlowable<T> RefCount<T>(this IConnectableFlowable<T> source, int count = 1)
        {
            // TODO implement
            throw new NotImplementedException();
        }

        public static IFlowableProcessor<T> RefCount<T>(this IFlowableProcessor<T> source)
        {
            if (source is FlowableProcessorRefCount<T>)
            {
                return source;
            }
            return new FlowableProcessorRefCount<T>(source);
        }

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

        public static IFlowable<T> DoOnNext<T>(this IFlowable<T> source, Action<T> onNext)
        {
            return FlowablePeek<T>.Create(source, onNext: onNext);
        }

        public static IFlowable<T> DoAfterNext<T>(this IFlowable<T> source, Action<T> onAfterNext)
        {
            return FlowablePeek<T>.Create(source, onAfterNext: onAfterNext);
        }

        public static IFlowable<T> DoOnError<T>(this IFlowable<T> source, Action<Exception> onError)
        {
            return FlowablePeek<T>.Create(source, onError: onError);
        }

        public static IFlowable<T> DoOnComplete<T>(this IFlowable<T> source, Action onComplete)
        {
            return FlowablePeek<T>.Create(source, onComplete: onComplete);
        }

        public static IFlowable<T> DoOnTerminated<T>(this IFlowable<T> source, Action onTerminated)
        {
            return FlowablePeek<T>.Create(source, onTerminated: onTerminated);
        }

        public static IFlowable<T> DoAfterTerminated<T>(this IFlowable<T> source, Action onAfterTerminated)
        {
            return FlowablePeek<T>.Create(source, onAfterTerminated: onAfterTerminated);
        }

        public static IFlowable<T> DoFinally<T>(this IFlowable<T> source, Action onFinally)
        {
            return new FlowableDoFinally<T>(source, onFinally);
        }

        public static IFlowable<T> DoOnSubscribe<T>(this IFlowable<T> source, Action<ISubscription> onSubscribe)
        {
            return FlowablePeek<T>.Create(source, onSubscribe: onSubscribe);
        }

        public static IFlowable<T> DoOnRequest<T>(this IFlowable<T> source, Action<long> onRequest)
        {
            return FlowablePeek<T>.Create(source, onRequest: onRequest);
        }

        public static IFlowable<T> DoOnCancel<T>(this IFlowable<T> source, Action onCancel)
        {
            return FlowablePeek<T>.Create(source, onCancel: onCancel);
        }

        public static IFlowable<T> DoOnPoll<T>(this IFlowable<T> source, Action<bool, T> onPoll)
        {
            // TODO implement
            throw new NotImplementedException();
        }

        // ********************************************************************************
        // Consumer methods
        // ********************************************************************************

        public static IDisposable Subscribe<T>(this IFlowable<T> source)
        {
            return Subscribe(source, v => { }, e => { }, () => { });
        }

        public static IDisposable Subscribe<T>(this IFlowable<T> source, Action<T> onNext)
        {
            return Subscribe(source, onNext, e => { }, () => { });
        }

        public static IDisposable Subscribe<T>(this IFlowable<T> source, Action<T> onNext, Action<Exception> onError)
        {
            return Subscribe(source, onNext, onError, () => { });
        }

        public static IDisposable Subscribe<T>(this IFlowable<T> source, Action<T> onNext, Action<Exception> onError, Action onComplete)
        {
            var s = new ActionSubscriber<T>(onNext, onError, onComplete);
            source.Subscribe(s);
            return s;
        }

        public static S SubscribeWith<T, S>(this IFlowable<T> source, S subscriber) where S : IFlowableSubscriber<T>
        {
            source.Subscribe(subscriber);
            return subscriber;
        }

        public static Task<T> FirstTask<T>(this IFlowable<T> source, CancellationTokenSource cts)
        {
            var s = new TaskFirstSubscriber<T>(cts);
            source.Subscribe(s);
            return s.Task;
        }

        public static Task<T> LastTask<T>(this IFlowable<T> source, CancellationTokenSource cts)
        {
            var s = new TaskLastSubscriber<T>(cts);
            source.Subscribe(s);
            return s.Task;
        }

        public static Task IgnoreElementsTask<T>(this IFlowable<T> source, CancellationTokenSource cts)
        {
            var s = new TaskIgnoreElementsSubscriber<T>(cts);
            source.Subscribe(s);
            return s.Task;
        }

        // ********************************************************************************
        // Blocking operators
        // ********************************************************************************

        public static bool BlockingFirst<T>(this IFlowable<T> source, out T result)
        {
            var s = new BlockingFirstSubscriber<T>();
            source.Subscribe(s);
            return s.BlockingGet(out result);
        }

        public static bool BlockingLast<T>(this IFlowable<T> source, out T result)
        {
            var s = new BlockingLastSubscriber<T>();
            source.Subscribe(s);
            return s.BlockingGet(out result);
        }

        public static IEnumerable<T> BlockingEnumerable<T>(this IFlowable<T> source)
        {
            return BlockingEnumerable(source, BufferSize());
        }

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

        public static void BlockingSubscribe<T>(this IFlowable<T> source)
        {
            BlockingSubscribe(source, v => { }, e => { }, () => { });
        }

        public static void BlockingSubscribe<T>(this IFlowable<T> source, Action<T> onNext)
        {
            BlockingSubscribe(source, onNext, e => { }, () => { });
        }

        public static void BlockingSubscribe<T>(this IFlowable<T> source, Action<T> onNext, Action<Exception> onError)
        {
            BlockingSubscribe(source, onNext, onError, () => { });
        }

        public static void BlockingSubscribe<T>(this IFlowable<T> source, Action<T> onNext, Action<Exception> onError, Action onComplete, Action<IDisposable> onConnect = null)
        {
            BlockingSubscribe(source, onNext, onError, onComplete, BufferSize(), onConnect);
        }

        public static void BlockingSubscribe<T>(this IFlowable<T> source, Action<T> onNext, Action<Exception> onError, Action onComplete, int prefetch, Action<IDisposable> onConnect = null)
        {
            var s = new BlockingLambdaSubscriber<T>(prefetch, onNext, onError, onComplete);
            onConnect?.Invoke(s);
            source.Subscribe(s);
            s.Run();
        }

        public static void BlockingSubscribe<T>(this IFlowable<T> source, IFlowableSubscriber<T> subscriber)
        {
            BlockingSubscribe(source, subscriber, BufferSize());
        }

        public static void BlockingSubscribe<T>(this IFlowable<T> source, IFlowableSubscriber<T> subscriber, int prefetch)
        {
            var s = new BlockingSubscriber<T>(prefetch, subscriber);
            source.Subscribe(s);
            s.Run();
        }

        // ********************************************************************************
        // Test methods
        // ********************************************************************************

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
