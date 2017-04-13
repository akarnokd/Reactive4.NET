using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Reactive4.NET.Test
{
    [TestFixture]
    public class ParallelFlowableToSortedListTest
    {
        [Test]
        public void Normal()
        {
            Flowable.Range(1, 10)
                .Map(v => 11 - v)
                .Parallel(2)
                .ToSortedList()
                .Test()
                .AssertResult(new List<int>(new int[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }));
        }

        [Test]
        public void Many()
        {
            for (int i = 0; i <= 100; i++)
            {
                for (int j = 1; j <= 32; j++)
                {
                    Flowable.Range(1, i)
                        .Map(v => i + 1 - v)
                        .Parallel(j)
                        .ToSortedList()
                        .Test()
                        .AssertValues(Enumerable.Range(1, i).ToArray())
                        .AssertNoError()
                        .AssertComplete();
                }
            }
        }

        [Test]
        public void ManyAsync()
        {
            for (int i = 0; i <= 100; i++)
            {
                for (int j = 1; j <= 32; j++)
                {
                    for (int k = 0; k < 20; k++)
                    {
                        Flowable.Range(1, i)
                            .Map(v => i + 1 - v)
                            .Parallel(j)
                            .RunOn(Executors.Computation)
                            .ToSortedList()
                            .Test()
                            .AwaitDone(TimeSpan.FromSeconds(5))
                            .AssertValues(Enumerable.Range(1, i).ToArray())
                            .AssertNoError()
                            .AssertComplete();
                    }
                }
            }
        }
    }
}
