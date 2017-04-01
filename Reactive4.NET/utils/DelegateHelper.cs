using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reactive4.NET.utils
{
    internal static class ListSupplier<T>
    {
        internal static readonly Func<IList<T>> Instance = () => new List<T>();
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
        internal static readonly Action<List<T>, T> Instance = (a, b) => a.Add(b);
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
}
