using NUnit.Framework;
using System;
using System.Linq;

namespace Reactive4.NET.Test.Direct
{
    [TestFixture]
    public class FlowableCombineLatestTest
    {
        [Test]
        public void Normal()
        {
            Flowable.CombineLatest(Flowable.Just(1), Flowable.Range(1, 5), (a, b) => a + b)
                .Test()
                .AssertResult(2, 3, 4, 5, 6);
        }

        [Test]
        public void NormalBackpressured()
        {
            Flowable.CombineLatest(Flowable.Just(1), Flowable.Range(1, 5), (a, b) => a + b)
                .Test(0)
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
