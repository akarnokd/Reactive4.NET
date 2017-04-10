using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reactive4.NET.utils
{
    internal static class ListSupplier<T>
    {
        internal static readonly Func<List<T>> Instance = () => new List<T>();
    }

    internal static class EmptyConsumer<T>
    {
        internal static readonly Action<T> Instance = v => { };
    }

    internal static class EmptyAction
    {
        internal static readonly Action Instance = () => { };
    }

    internal static class Identity<T>
    {
        internal static readonly Func<T, T> Instance = v => v;
    }

    internal static class Func2Second<T, U>
    {
        internal static readonly Func<T, U, U> Instance = (a, b) => b;
    }

    internal static class ListAdd<T>
    {
        internal static readonly Action<IList<T>, T> Instance = (a, b) => a.Add(b);
    }

    internal static class IntAdd
    {
        internal static readonly Func<int, int, int> Instance = (a, b) => a + b;
    }

    internal static class LongAdd
    {
        internal static readonly Func<long, long, long> Instance = (a, b) => a + b;
    }

    internal static class IntMax
    {
        internal static readonly Func<int, int, int> Instance = (a, b) => Math.Max(a, b);
    }

    internal static class IntMin
    {
        internal static readonly Func<int, int, int> Instance = (a, b) => Math.Min(a, b);
    }

    internal static class LongMax
    {
        internal static readonly Func<long, long, long> Instance = (a, b) => Math.Max(a, b);
    }

    internal static class LongMin
    {
        internal static readonly Func<long, long, long> Instance = (a, b) => Math.Min(a, b);
    }

    internal static class AlwaysFalse<T>
    {
        internal static readonly Func<T, bool> Instance = v => false;
    }

    internal static class AlwaysFalse
    {
        internal static readonly Func<bool> Instance = () => false;
    }

    internal static class AlwaysTrue<T>
    {
        internal static readonly Func<T, bool> Instance = v => true;
    }

    internal static class AlwaysTrue
    {
        internal static readonly Func<bool> Instance = () => true;
    }

    internal static class PublishProcessorSupplier<T>
    {
        internal static readonly Func<PublishProcessor<T>> Instance = () => new PublishProcessor<T>();
    }

    internal static class ReplayProcessorSupplier<T>
    {
        internal static readonly Func<ReplayProcessor<T>> Instance = () => new ReplayProcessor<T>();
    }

    internal static class ListSort<T>
    {
        internal static readonly Action<List<T>> AsAction = list => list.Sort();

        internal static readonly Func<List<T>, IList<T>> AsFunction = list => { list.Sort(); return list; };
    }

    internal static class MergeLists<T>
    {
        internal static readonly Func<IList<T>, IList<T>, IList<T>> Instance = (a, b) => Merge(a, b, Comparer<T>.Default);

        internal static IList<T> Merge(IList<T> first, IList<T> second, IComparer<T> comparer)
        {
            if (first.Count == 0)
            {
                return second;
            }
            if (second.Count == 0)
            {
                return first;
            }

            IList<T> result = new List<T>(first.Count + second.Count);



            return result;
        }
    }

    internal static class ParallelFailureHandler
    {
        internal static readonly Func<long, Exception, ParallelFailureMode> Error = (a, b) => ParallelFailureMode.Error;

        internal static readonly Func<long, Exception, ParallelFailureMode> Skip = (a, b) => ParallelFailureMode.Skip;

        internal static readonly Func<long, Exception, ParallelFailureMode> Complete = (a, b) => ParallelFailureMode.Complete;

        internal static readonly Func<long, Exception, ParallelFailureMode> Retry = (a, b) => ParallelFailureMode.Retry;

        internal static readonly Func<long, Exception, ParallelFailureMode>[] Values =
        {
            Error, Skip, Complete, Retry
        };
    }
}
