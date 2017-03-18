using NUnit.Framework;
using System;
using System.Linq;

namespace Reactive4.NET.Test
{
    [TestFixture]
    public class FlowableTakeLastTest
    {
        [Test]
        public void Normal()
        {
            Flowable.Range(1, 10)
                .TakeLast(5)
                .Test()
                .AssertResult(6, 7, 8, 9, 10);
        }

        [Test]
        public void NormalBackpressured()
        {
            Flowable.Range(1, 10)
                .TakeLast(5)
                .Test(0)
                .AssertValues()
                .RequestMore(1)
                .AssertValues(6)
                .RequestMore(2)
                .AssertValues(6, 7, 8)
                .RequestMore(2)
                .AssertResult(6, 7, 8, 9, 10);
        }
    }
}
