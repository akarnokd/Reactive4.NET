using NUnit.Framework;
using System;
using System.Linq;

namespace Reactive4.NET.Test
{
    [TestFixture]
    public class FlowableDebounceTest
    {
        [Test]
        public void Normal()
        {
            Flowable.Range(1, 10).Debounce(TimeSpan.FromMinutes(1))
                .Test()
                .AwaitDone(TimeSpan.FromSeconds(5))
                .AssertResult(10);
        }

        [Test]
        public void Lockstep()
        {
            var tex = new TestExecutor();
            var dp = new DirectProcessor<int>();

            var ts = dp.Debounce(TimeSpan.FromMilliseconds(5), tex).Test();

            ts.AssertEmpty();

            dp.OnNext(1);

            ts.AssertEmpty();

            tex.AdvanceTimeBy(TimeSpan.FromMilliseconds(5));

            ts.AssertValues(1)
                .AssertNoError()
                .AssertNotComplete();

            dp.OnNext(2);

            ts.AssertValues(1)
                .AssertNoError()
                .AssertNotComplete();

            tex.AdvanceTimeBy(TimeSpan.FromMilliseconds(2));

            ts.AssertValues(1)
                .AssertNoError()
                .AssertNotComplete();

            dp.OnNext(3);

            tex.AdvanceTimeBy(TimeSpan.FromMilliseconds(5));

            ts.AssertValues(1, 3)
                .AssertNoError()
                .AssertNotComplete();

            tex.AdvanceTimeBy(TimeSpan.FromMilliseconds(2));

            dp.OnNext(4);

            tex.AdvanceTimeBy(TimeSpan.FromMilliseconds(2));

            dp.OnComplete();

            tex.AdvanceTimeBy(TimeSpan.FromMilliseconds(1));

            ts.AssertResult(1, 3, 4);
        }
    }
}
