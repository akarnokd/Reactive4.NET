using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Reactive4.NET.Test.Direct
{
    [TestFixture]
    public class FlowableConcatEnumerableTest
    {

        IEnumerable<R> FromArray<R>(params R[] array)
        {
            return array;
        }

        [Test]
        public void Normal()
        {
            Flowable.Concat(FromArray(Flowable.Range(1, 3), Flowable.Range(4, 3)))
                .Test()
                .AssertResult(1, 2, 3, 4, 5, 6);
        }

        [Test]
        public void NormalConditional()
        {
            Flowable.Concat(FromArray(Flowable.Range(1, 3), Flowable.Range(4, 3)))
                .Filter(v => true)
                .Test()
                .AssertResult(1, 2, 3, 4, 5, 6);
        }

        [Test]
        public void Rebatch()
        {
            Flowable.Concat(FromArray(Flowable.Range(1, 3), Flowable.Range(4, 3)))
                .RebatchRequests(1)
                .Test()
                .AssertResult(1, 2, 3, 4, 5, 6);
        }

        [Test]
        public void Take()
        {
            Flowable.Concat(FromArray(Flowable.Range(1, 3), Flowable.Range(4, 3)))
                .Take(4)
                .Test()
                .AssertResult(1, 2, 3, 4);
        }

        [Test]
        public void Cancel()
        {
            var dp = new DirectProcessor<int>();
            var ts = Flowable.Concat(FromArray(dp)).Test();

            Assert.IsTrue(dp.HasSubscribers);

            ts.Cancel();

            Assert.IsFalse(dp.HasSubscribers);
        }

        [Test]
        public void Backpressure()
        {
            Flowable.Concat(FromArray(Flowable.Range(1, 3), Flowable.Range(4, 3)))
                .Test(0)
                .AssertValues()
                .RequestMore(1)
                .AssertValues(1)
                .RequestMore(3)
                .AssertValues(1, 2, 3, 4)
                .RequestMore(2)
                .AssertResult(1, 2, 3, 4, 5, 6);
        }
    }
}
