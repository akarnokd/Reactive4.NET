using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Reactive4.NET.Test.Direct
{
    [TestFixture]
    public class FlowableBufferBoundaryTest
    {
        [Test]
        public void Normal()
        {
            var dp1 = new DirectProcessor<int>();
            var dp2 = new DirectProcessor<int>();

            var ts = dp1.Buffer(dp2).Test();

            ts.AssertEmpty();

            dp1.OnNext(1);
            dp1.OnNext(2);

            dp2.OnNext(100);

            ts.AssertValues(
                new List<int>(new int[] { 1, 2 })
            )
            .AssertNoError()
            .AssertNotComplete();

            dp2.OnNext(200);

            ts.AssertValues(
                new List<int>(new int[] { 1, 2 }),
                new List<int>()
            )
            .AssertNoError()
            .AssertNotComplete();

            dp1.OnNext(3);
            dp1.OnNext(4);
            dp1.OnNext(5);

            dp1.OnComplete();

            ts.AssertResult(
                new List<int>(new int[] { 1, 2 }),
                new List<int>(),
                new List<int>(new int[] { 3, 4, 5 })
            );

            Assert.IsFalse(dp2.HasSubscribers);
        }
    }
}
