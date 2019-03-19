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
        public void SourceErrorDelayed()
        {
            var seen = new List<int>();
            Flowable.Just(0)
                .ConcatWith(Flowable.Error<int>(new Exception()))
                .FlatMap(x => Flowable.Range(0, 5).Delay(TimeSpan.FromMilliseconds(10)))
                .DoOnNext(seen.Add)
                .Test()
                .AwaitDone(TimeSpan.FromSeconds(5))
                .AssertError(typeof(Exception));

            CollectionAssert.AreEqual(Enumerable.Range(0, 5), seen);
        }
        
        [Test]
        public void SourceErrorEager()
        {
            var seen = new List<int>();
            Flowable.Just(0)
                .ConcatWith(Flowable.Error<int>(new Exception()))
                .FlatMap(x => Flowable.Range(0, 5).Delay(TimeSpan.FromMilliseconds(10)), Flowable.BufferSize(), Flowable.BufferSize(), false)
                .DoOnNext(seen.Add)
                .Test()
                .AwaitDone(TimeSpan.FromSeconds(5))
                .AssertError(typeof(Exception));

            CollectionAssert.IsEmpty(seen);
        }
        
        [Test]
        public void InnerErrorDelayed()
        {
            var seen = new List<int>();
            Flowable.Range(0, 5)
                .FlatMap(x => x == 0 ? Flowable.Error<int>(new Exception()) : Flowable.Just(x).Delay(TimeSpan.FromMilliseconds(10)))
                .DoOnNext(seen.Add)
                .Test()
                .AwaitDone(TimeSpan.FromSeconds(5))
                .AssertError(typeof(Exception));
            
            CollectionAssert.AreEquivalent(Enumerable.Range(1, 4), seen);
        }
        
        [Test]
        public void InnerErrorEager()
        {
            var seen = new List<int>();
            Flowable.Range(0, 5)
                .FlatMap(x => x == 0 ? Flowable.Error<int>(new Exception()) : Flowable.Just(x).Delay(TimeSpan.FromMilliseconds(10)), Flowable.BufferSize(), Flowable.BufferSize(), false)
                .DoOnNext(seen.Add)
                .Test()
                .AwaitDone(TimeSpan.FromSeconds(5))
                .AssertError(typeof(Exception));
            
            CollectionAssert.IsEmpty(seen);
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
    }
}
