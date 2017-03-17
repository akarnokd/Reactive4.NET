using Reactive4.NET.utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Reactive.Streams;

namespace Reactive4.NET.Test
{
    public sealed class Benchmark
    {
        static object result;

        public static void Run(Func<object> func, string name, int warmup = 5, int measure = 5, bool showrounds = false)
        {
            int n = warmup + measure;
            long[] operations = new long[n];
            long[] times = new long[n];

            for (int i = 0; i < n; i++)
            {
                long start = SchedulerHelper.NowUTC();
                long end;
                long ops = 0L;
                long tdiff;
                do
                {
                    Volatile.Write(ref result, func());
                    ops++;
                }
                while ((tdiff = (end = SchedulerHelper.NowUTC()) - start) < 1000);

                operations[i] = ops;
                times[i] = tdiff;
                if (showrounds)
                {
                    Console.WriteLine(string.Format("# {0,2} | {1}: {2:#,0.000} ops/s, {3:#,0.000} ms/op", i + 1, name, ops * 1000d / tdiff, tdiff * 1d / ops));
                }
            }

            long sumOps = 0L;
            long sumTimes = 0L;
            for (int i = warmup; i < n; i++)
            {
                sumOps += operations[i];
                sumTimes += times[i];
            }

            Console.WriteLine(string.Format("{0}: {1:#,0.000} ops/s, {2:#,0.000} ms/op", name, sumOps * 1000d / sumTimes, sumTimes * 1d / sumOps));
        }
    }

    public sealed class PerfFlowableSubscriber<T> : IFlowableSubscriber<T>
    {
        bool done;
        Exception error;
        T item;
        bool hasItem;
        ISubscription upstream;

        public void OnComplete()
        {
            Volatile.Write(ref done, true);
        }

        public void OnError(Exception cause)
        {
            Volatile.Write(ref error, cause);
        }

        public void OnNext(T element)
        {
            item = element;
            Volatile.Write(ref hasItem, true);
        }

        public void OnSubscribe(ISubscription subscription)
        {
            Volatile.Write(ref upstream, subscription);
            subscription.Request(long.MaxValue);
        }
    }

    public sealed class PerfSubscriber<T> : ISubscriber<T>
    {
        bool done;
        Exception error;
        T item;
        bool hasItem;
        ISubscription upstream;

        public void OnComplete()
        {
            Volatile.Write(ref done, true);
        }

        public void OnError(Exception cause)
        {
            Volatile.Write(ref error, cause);
        }

        public void OnNext(T element)
        {
            item = element;
            Volatile.Write(ref hasItem, true);
        }

        public void OnSubscribe(ISubscription subscription)
        {
            Volatile.Write(ref upstream, subscription);
            subscription.Request(long.MaxValue);
        }
    }
}
