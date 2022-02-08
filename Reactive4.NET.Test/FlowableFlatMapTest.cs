using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Reactive4.NET.Test.Direct
{
    [TestFixture]
    public class FlowableFlatMapTest
    {
        [Test]
        public void SourceErrorDelayed()
        {
            Flowable.Just(0)
                .ConcatWith(Flowable.Error<int>(new Exception()))
                .FlatMap(x => Flowable.Range(0, 5).Delay(TimeSpan.FromMilliseconds(10)))
                .Test()
                .AwaitDone(TimeSpan.FromSeconds(5))
                .AssertValues(Enumerable.Range(0, 5))
                .AssertError(typeof(Exception));
        }
        
        [Test]
        public void SourceErrorEager()
        {
            Flowable.Just(0)
                .ConcatWith(Flowable.Error<int>(new Exception()))
                .FlatMap(x => Flowable.Range(0, 5).Delay(TimeSpan.FromMilliseconds(10)), Flowable.BufferSize(), Flowable.BufferSize(), false)
                .Test()
                .AwaitDone(TimeSpan.FromSeconds(5))
                .AssertValueCount(0)
                .AssertError(typeof(Exception));
        }
        
        [Test]
        public void InnerErrorDelayed()
        {
            Flowable.Range(0, 5)
                .FlatMap(x => x == 0 ? Flowable.Error<int>(new Exception()) : Flowable.Just(x).Delay(TimeSpan.FromMilliseconds(10)))
                .Test()
                .AwaitDone(TimeSpan.FromSeconds(5))
                .AssertValueCount(4)
                .AssertError(typeof(Exception));
        }
        
        [Test]
        public void InnerErrorEager()
        {
            Flowable.Range(0, 5)
                .FlatMap(x => x == 0 ? Flowable.Error<int>(new Exception()) : Flowable.Just(x).Delay(TimeSpan.FromMilliseconds(10)), Flowable.BufferSize(), Flowable.BufferSize(), false)
                .Test()
                .AwaitDone(TimeSpan.FromSeconds(5))
                .AssertValueCount(0)
                .AssertError(typeof(Exception));
        }

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

        [Test]
        public void MergeNoDelayError()
        {
            var delayError = false;
            var ok = Flowable.Timer(TimeSpan.FromMilliseconds(1000));
            var errRx = Flowable.Interval(TimeSpan.FromMilliseconds(100)).Take(2)
                .ConcatWith(Flowable.Error<long>(new Exception("wtf")));
            Flowable.FromArray(ok, errRx).Merge(42, 42, delayError)
                .Test()
                .AwaitDone(TimeSpan.FromSeconds(5))
                .AssertFailureAndMessage(typeof(Exception), "wtf", 0L, 1L);
        }
    }
}
