using NUnit.Framework;
using System;
using System.Linq;

namespace Reactive4.NET.Test.Direct
{
    [TestFixture]
    public class FlowableReduceTest
    {
        [Test]
        public void Normal()
        {
            Flowable.Range(1, 5).Reduce((a, b) => a + b)
                .Test()
                .AssertResult(15);
        }

        [Test]
        public void Normal2()
        {
            Flowable.Range(1, 5).Map(v => 0).Reduce((a, b) => a + b)
                .Test()
                .AssertResult(0);
        }

        [Test]
        [Timeout(5000)]
        public void Normal3()
        {
            Flowable.Range(1, 5).Map(v => 0).Reduce((a, b) => a + b)
                .BlockingFirst(out int sum);

            Assert.AreEqual(0, sum);
        }
    }
}
