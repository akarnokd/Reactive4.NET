using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Reactive4.NET.Test
{
    [TestFixture]
    public class FlowableFlatMapTest
    {
        [Test]
        public void Simple()
        {
            Flowable.Range(1, 5)
                .FlatMap(v => Flowable.Range(v, 2))
                .Test()
                .AssertResult(1, 2, 2, 3, 3, 4, 4, 5, 5, 6);
        }

        //[Test]
        public void AsyncMergeLoop()
        {
            for (int i = 0; i < 20; i++)
            {
                AsyncMerge();
            }
        }

        [Test]
        public void AsyncMerge()
        {
            var maxConcurrency = 2;
            var prefetch = Flowable.BufferSize();
            var backpressureStrategy = BackpressureStrategy.BUFFER;

            var flowable = Flowable.Range(0, 5).FlatMap(x =>
            {
                return Flowable.Create<int>(e =>
                {
                // Thread to emulate some asynchronous action
                new Thread(() =>
                    {
                        for (var i = 0; i < 10; ++i)
                        {
                            Thread.Sleep(100);

                            e.OnNext(x * i);
                        }
                        e.OnComplete();
                    }).Start();
                }, backpressureStrategy);
            }, maxConcurrency, prefetch);

            var v = flowable
                .Test()
                .AwaitDone(TimeSpan.FromSeconds(6))
                .AssertNoError()
                .AssertComplete()
                .Values
                ;

            Assert.AreEqual(50, v.Count);
        }
    }
}
