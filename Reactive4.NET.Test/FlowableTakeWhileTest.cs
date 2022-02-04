using NUnit.Framework;
using System;
using System.Linq;

namespace Reactive4.NET.Test.Direct
{
    [TestFixture]
    public class FlowableTakeWhileTest
    {
        [Test]
        public void Normal()
        {
            Flowable.Range(1, 10)
                .TakeWhile(v => v <= 5)
                .Test()
                .AssertResult(1, 2, 3, 4, 5);
        }

        [Test]
        public void NormalAll()
        {
            Flowable.Range(1, 5)
                .TakeWhile(v => true)
                .Test()
                .AssertResult(1, 2, 3, 4, 5);
        }

        [Test]
        public void NormalBackpressured()
        {
            Flowable.Range(1, 10)
                .TakeWhile(v => v <= 5)
                .Test(0)
                .AssertValues()
                .RequestMore(1)
                .AssertValues(1)
                .RequestMore(2)
                .AssertValues(1, 2, 3)
                .RequestMore(6)
                .AssertResult(1, 2, 3, 4, 5);
        }

        [Test]
        public void NormalAllBackpressured()
        {
            Flowable.Range(1, 5)
                .TakeWhile(v => true)
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
