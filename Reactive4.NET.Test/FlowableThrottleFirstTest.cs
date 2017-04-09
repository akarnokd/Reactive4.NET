using NUnit.Framework;
using System;
using System.Linq;

namespace Reactive4.NET.Test
{
    [TestFixture]
    public class FlowableThrottleFirstTest
    {
        [Test]
        public void Normal()
        {
            var tex = new TestExecutor();

            var dp = new DirectProcessor<int>();

            var ts = dp.ThrottleFirst(TimeSpan.FromMilliseconds(5), tex).Test();

            ts.AssertEmpty();

            dp.OnNext(1);

            ts.AssertValues(1)
                .AssertNoError()
                .AssertNotComplete();

            dp.OnNext(2);

            ts.AssertValues(1)
            .AssertNoError()
            .AssertNotComplete();

            tex.AdvanceTimeBy(TimeSpan.FromMilliseconds(5));

            ts.AssertValues(1)
            .AssertNoError()
            .AssertNotComplete();

            dp.OnNext(3);

            ts.AssertValues(1, 3)
            .AssertNoError()
            .AssertNotComplete();

            dp.OnNext(4);
            dp.OnComplete();

            ts.AssertResult(1, 3);

            tex.AdvanceTimeBy(TimeSpan.FromMilliseconds(5));

            ts.AssertResult(1, 3);
        }
        [Test]
        public void NormalConditional()
        {
            var tex = new TestExecutor();

            var dp = new DirectProcessor<int>();

            var ts = dp.ThrottleFirst(TimeSpan.FromMilliseconds(5), tex).Filter(v => true).Test();

            ts.AssertEmpty();

            dp.OnNext(1);

            ts.AssertValues(1)
                .AssertNoError()
                .AssertNotComplete();

            dp.OnNext(2);

            ts.AssertValues(1)
            .AssertNoError()
            .AssertNotComplete();

            tex.AdvanceTimeBy(TimeSpan.FromMilliseconds(5));

            ts.AssertValues(1)
            .AssertNoError()
            .AssertNotComplete();

            dp.OnNext(3);

            ts.AssertValues(1, 3)
            .AssertNoError()
            .AssertNotComplete();

            dp.OnNext(4);
            dp.OnComplete();

            ts.AssertResult(1, 3);

            tex.AdvanceTimeBy(TimeSpan.FromMilliseconds(5));

            ts.AssertResult(1, 3);
        }
    }
}
