using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Reactive4.NET.Test
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
    }
}
