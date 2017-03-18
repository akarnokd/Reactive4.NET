using NUnit.Framework;
using System;
using System.Linq;

namespace Reactive4.NET.Test
{
    [TestFixture]
    public class FlowableSkipLastTest
    {
        [Test]
        public void Normal()
        {
            Flowable.Range(1, 10)
                .SkipLast(5)
                .Test()
                .AssertResult(1, 2, 3, 4, 5);
        }

        [Test]
        public void NormalBackpressured()
        {
            Flowable.Range(1, 10)
                .SkipLast(5)
                .Test(0)
                .AssertValues()
                .RequestMore(1)
                .AssertValues(1)
                .RequestMore(2)
                .AssertValues(1, 2, 3)
                .RequestMore(2)
                .AssertResult(1, 2, 3, 4, 5);
        }
    }
}
