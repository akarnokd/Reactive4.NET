﻿using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Reactive4.NET.Test.Direct
{
    [TestFixture]
    public class ParallelFlowableTest
    {
        [Test]
        public void Zero1()
        {
            Flowable.Empty<int>()
                .Parallel(1)
                .Sequential()
                .Test()
                .AssertResult();
        }

        [Test]
        public void Zero2()
        {
            Flowable.Empty<int>()
                .Parallel(2)
                .Sequential()
                .Test()
                .AssertResult();
        }

        [Test]
        public void Zero2Hide()
        {
            Flowable.Empty<int>()
                .Hide()
                .Parallel(2)
                .Sequential()
                .Test()
                .AssertResult();
        }

        [Test]
        public void One()
        {
            Flowable.Just(1)
                .Parallel(1)
                .Sequential()
                .Test()
                .AssertResult(1);
        }

        [Test]
        public void Sync()
        {
            for (int j = 0; j <= 100; j++)
            {
                for (int i = 1; i < 33; i++)
                {
                    Flowable.Range(1, j)
                        .Parallel(i)
                        .Sequential()
                        .Test()
                        .WithTag("len=" + j + ", i=" + i)
                        .AssertValueCount(j)
                        .AssertValues(Enumerable.Range(1, j))
                        .AssertNoError()
                        .AssertComplete();
                }
            }
        }

        [Test]
        public void SyncHidden()
        {
            for (int j = 0; j <= 100; j++) {
                for (int i = 1; i < 33; i++)
                {
                    Flowable.Range(1, j)
                        .Hide()
                        .Parallel(i)
                        .Sequential()
                        .Test()
                        .WithTag("len=" + j + ", i=" + i)
                        .AssertValues(Enumerable.Range(1, j))
                        .AssertNoError()
                        .AssertComplete();
                }
            }
        }

        [Test]
        public void Async()
        {
            for (int j = 0; j <= 100; j++)
            {
                for (int i = 1; i < 33; i++)
                {
                    for (int k = 0; k < 20; k++)
                    {
                        Flowable.Range(1, j)
                            .Parallel(i)
                            .RunOn(Executors.Computation)
                            .Sequential()
                            .Test()
                            .WithTag("len=" + j + ", i=" + i)
                            .AwaitDone(TimeSpan.FromSeconds(5))
                            .AssertValueCount(j)
                            .AssertValueSet(new HashSet<int>(Enumerable.Range(1, j)))
                            .AssertNoError()
                            .AssertComplete();
                    }
                }
            }
        }

        [Test]
        public void AsyncHidden()
        {
            for (int j = 0; j <= 100; j++)
            {
                for (int i = 1; i < 33; i++)
                {
                    Flowable.Range(1, j)
                        .Hide()
                        .Parallel(i)
                        .RunOn(Executors.Computation)
                        .Sequential()
                        .Test()
                        .WithTag("len=" + j + ", i=" + i)
                        .AwaitDone(TimeSpan.FromSeconds(5))
                        .AssertValueCount(j)
                        .AssertValueSet(new HashSet<int>(Enumerable.Range(1, j)))
                        .AssertNoError()
                        .AssertComplete();
                }
            }
        }

        [Test]
        [Repeat(1000)]
        public void AsyncHidden2()
        {
            var j = 2294;
            var i = Environment.ProcessorCount;
            var completions = 0;

            try
            {
                Flowable.Range(1, j)
                    .Hide()
                    .Parallel(i)
                    .DoOnComplete(() => Interlocked.Increment(ref completions))
                    .RunOn(Executors.Computation)
                    .Sequential()
                    .Test()
                    .WithTag("len=" + j + ", i=" + i)
                    .AwaitDone(TimeSpan.FromSeconds(5))
                    .AssertValueCount(j)
                    .AssertValueSet(new HashSet<int>(Enumerable.Range(1, j)))
                    .AssertNoError()
                    .AssertComplete();
            }
            finally
            {
                Assert.AreEqual(i, Volatile.Read(ref completions));
            }
        }

        [Test]
        public void Map()
        {
            for (int j = 0; j <= 100; j++)
            {
                for (int i = 1; i < 33; i++)
                {
                    Flowable.Range(1, j)
                        .Parallel(i)
                        .Map(v => v + 1)
                        .Sequential()
                        .Test()
                        .WithTag("len=" + j + ", i=" + i)
                        .AssertValueCount(j)
                        .AssertValues(Enumerable.Range(2, j))
                        .AssertNoError()
                        .AssertComplete();
                }
            }
        }

        [Test]
        public void MapConditional()
        {
            for (int j = 0; j <= 100; j++)
            {
                for (int i = 1; i < 33; i++)
                {
                    Flowable.Range(1, j)
                        .Parallel(i)
                        .Map(v => v + 1)
                        .Filter(v => true)
                        .Sequential()
                        .Test()
                        .WithTag("len=" + j + ", i=" + i)
                        .AssertValueCount(j)
                        .AssertValues(Enumerable.Range(2, j))
                        .AssertNoError()
                        .AssertComplete();
                }
            }
        }

        [Test]
        public void Filter()
        {
            for (int j = 0; j <= 100; j++)
            {
                for (int i = 1; i < 33; i++)
                {
                    Flowable.Range(1, j)
                        .Parallel(i)
                        .Filter(v => true)
                        .Sequential()
                        .Test()
                        .WithTag("len=" + j + ", i=" + i)
                        .AssertValueCount(j)
                        .AssertValues(Enumerable.Range(1, j))
                        .AssertNoError()
                        .AssertComplete();
                }
            }
        }

        [Test]
        public void FilterConditional()
        {
            for (int j = 0; j <= 100; j++)
            {
                for (int i = 1; i < 33; i++)
                {
                    Flowable.Range(1, j)
                        .Parallel(i)
                        .Filter(v => true)
                        .Filter(v => true)
                        .Sequential()
                        .Test()
                        .WithTag("len=" + j + ", i=" + i)
                        .AssertValueCount(j)
                        .AssertValues(Enumerable.Range(1, j))
                        .AssertNoError()
                        .AssertComplete();
                }
            }
        }

        [Test]
        public void SyncReduceAll()
        {
            for (int j = 0; j <= 100; j++)
            {
                for (int i = 1; i < 33; i++)
                {
                    long sum = (1 + j) * j / 2;
                    var ts = Flowable.Range(1, j)
                        .Parallel(i)
                        .Map(v => (long)v)
                        .ReduceAll((a, b) => a + b)
                        .Test()
                        .WithTag("len = " + j + ", parallel = " + i)
                        ;
                    if (j == 0)
                    {
                        ts.AssertResult();
                    } else { 
                        ts.AssertResult(sum);
                    }
                }
            }
        }

        [Test]
        public void AsyncReduceAll()
        {
            for (int j = 0; j <= 100; j++)
            {
                for (int i = 1; i < 33; i++)
                {
                    long sum = (1 + j) * j / 2;
                    var ts = Flowable.Range(1, j)
                        .Parallel(i)
                        .RunOn(Executors.Computation)
                        .Map(v => (long)v)
                        .ReduceAll((a, b) => a + b)
                        .Test()
                        .AwaitDone(TimeSpan.FromSeconds(5))
                        .WithTag("len = " + j + ", parallel = " + i)
                        ;
                    if (j == 0)
                    {
                        ts.AssertResult();
                    }
                    else
                    {
                        ts.AssertResult(sum);
                    }
                }
            }
        }

        [Test]
        public void Sorted32()
        {
            int j = 3;
            int i = 2;

            Flowable.Range(1, j)
                .Map(v => j - v + 1)
                .Parallel(i)
                .Sorted()
                .Test()
                .WithTag("len=" + j + ", i=" + i)
                .AssertValueCount(j)
                .AssertValues(Enumerable.Range(1, j))
                .AssertNoError()
                .AssertComplete();

        }

        [Test]
        public void Sorted()
        {
            for (int j = 0; j <= 100; j++)
            {
                for (int i = 1; i < 33; i++)
                {
                    Flowable.Range(1, j)
                        .Map(v => j - v + 1)
                        .Parallel(i)
                        .Sorted()
                        .Test()
                        .WithTag("len=" + j + ", i=" + i)
                        .AssertValueCount(j)
                        .AssertValues(Enumerable.Range(1, j))
                        .AssertNoError()
                        .AssertComplete();
                }
            }
        }

        [Test]
        public void SortedP1S1()
        {
            int i = 1;
            int j = 0;
            Flowable.Range(1, j)
                        .Map(v => j - v + 1)
                        .Parallel(i)
                        .Sorted()
                        .Test()
                        .WithTag("len=" + j + ", i=" + i)
                        .AssertValueCount(j)
                        .AssertValues(Enumerable.Range(1, j))
                        .AssertNoError()
                        .AssertComplete();
        }
    }
}
