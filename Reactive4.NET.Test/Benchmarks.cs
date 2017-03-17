using NUnit.Framework;
using Reactive4.NET.utils;
using System;
using System.Linq;

namespace Reactive4.NET.Test
{
    [TestFixture]
    public class Benchmarks
    {
        //[Test]
        public void SubscriptionArbiter()
        {
            {
                var sa = new SubscriptionArbiter();
                var bs = new BooleanSubscription();
                Benchmark.Run(() =>
                {
                    for (int i = 0; i < 1000; i++)
                    {
                        sa.ArbiterSet(bs);
                    }
                    return null;
                }, "Set");

            }

            {
                var sa = new SubscriptionArbiter();
                Benchmark.Run(() =>
                {
                    for (int i = 0; i < 1000; i++)
                    {
                        sa.Request(1);
                    }
                    return null;
                }, "Request");
            }

            {
                var sa = new SubscriptionArbiter();
                var bs = new BooleanSubscription();
                sa.ArbiterSet(bs);
                Benchmark.Run(() =>
                {
                    for (int i = 0; i < 1000; i++)
                    {
                        sa.Request(1);
                    }
                    return null;
                }, "Request-Upstream");
            }

            {
                var sa = new SubscriptionArbiter();
                sa.Request(long.MaxValue - 1);
                Benchmark.Run(() =>
                {
                    for (int i = 0; i < 1000; i++)
                    {
                        sa.ArbiterProduced(1);
                    }
                    return null;
                }, "Produced");
            }
        }

        //[Test]
        public void Baseline()
        {
            Benchmark.Run(() => null, "Baseline");
        }

        //[Test]
        [Timeout(90000)]
        public void FlatMapEnumerable()
        {
            for (int i = 1; i <= 1000000; i *= 10) {
                int j = 1000000 / i;
                Benchmark.Run(() => {
                    Flowable.Range(1, i)
                    .FlatMapEnumerable(v => Enumerable.Range(v, j))
                    .Subscribe(new PerfFlowableSubscriber<int>());

                    return null;
                }, string.Format("FlatMapEnumerable: {0,6:#,##0} x {1,6:#,##0}", i, j));
            }
        }
        //[Test]
        [Timeout(90000)]
        public void FlatMapFromEnumerable()
        {
            for (int i = 1; i <= 1000000; i *= 10)
            {
                int j = 1000000 / i;
                Benchmark.Run(() => {
                    Flowable.Range(1, i)
                    .FlatMap(v => Flowable.FromEnumerable(Enumerable.Range(v, j)))
                    .Subscribe(new PerfFlowableSubscriber<int>());

                    return null;
                }, string.Format("FlatMapEnumerable: {0,6:#,##0} x {1,6:#,##0}", i, j));
            }
        }
    }
}
