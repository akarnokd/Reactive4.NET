using NUnit.Framework;
using System;
using System.Linq;

namespace Reactive4.NET.Test
{
    [TestFixture]
    public class FlowableConcatMapTest
    {
        [Test]
        public void Normal()
        {
            Flowable.Range(1, 5).ConcatMap(v => Flowable.Just(v + 1))
                .Test()
                .AssertResult(2, 3, 4, 5, 6);
        }

        [Test]
        public void NormalHide()
        {
            Flowable.Range(1, 5).Hide().ConcatMap(v => Flowable.Just(v + 1))
                .Test()
                .AssertResult(2, 3, 4, 5, 6);
        }

        [Test]
        public void NormalBackpressured()
        {
            Flowable.Range(1, 5).ConcatMap(v => Flowable.Just(v + 1))
                .Test(0L)
                .AssertValues()
                .RequestMore(1)
                .AssertValues(2)
                .RequestMore(2)
                .AssertValues(2, 3, 4)
                .RequestMore(2)
                .AssertResult(2, 3, 4, 5, 6);
        }
    }
}
