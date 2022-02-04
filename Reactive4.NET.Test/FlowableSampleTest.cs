using NUnit.Framework;
using System;
using System.Linq;
using System.Threading;

namespace Reactive4.NET.Test.Direct
{
    [TestFixture]
    public class FlowableSampleTest
    {
        [Test]
        [Timeout(10000)]
        public void Normal()
        {
            Flowable.Interval(TimeSpan.FromMilliseconds(2))
                .Map(v => {
                    Console.WriteLine(1);
                    return 1;
                })
                .Sample(TimeSpan.FromMilliseconds(10))
                .Take(10)
                .Test()
                .AwaitCount(10, () => Thread.Sleep(10), TimeSpan.FromSeconds(5))
                .ThenCancel()
                .AssertValues(1, 1, 1, 1, 1, 1, 1, 1, 1, 1)
                .AssertNoError();
        }

        [Test]
        public void Lockstep()
        {
            var dp1 = new DirectProcessor<int>();
            var dp2 = new DirectProcessor<int>();

            TestSubscriber<int> ts = dp1.Sample(dp2).Test();

            ts.AssertEmpty();

            dp1.OnNext(1);
            dp1.OnNext(2);

            dp2.OnNext(100);

            ts.AssertValues(2);

            dp2.OnNext(100);

            ts.AssertValues(2);

            dp1.OnNext(3);
            dp1.OnNext(4);

            dp2.OnNext(200);

            ts.AssertValues(2, 4);

            dp2.OnComplete();

            ts.AssertResult(2, 4);

            Assert.IsFalse(dp1.HasSubscribers);
            Assert.IsFalse(dp2.HasSubscribers);
        }

        [Test]
        public void LockstepEmitLast()
        {
            var dp1 = new DirectProcessor<int>();
            var dp2 = new DirectProcessor<int>();

            TestSubscriber<int> ts = dp1.Sample(dp2, true).Test();

            ts.AssertEmpty();

            dp1.OnNext(1);
            dp1.OnNext(2);

            dp2.OnNext(100);

            ts.AssertValues(2);

            dp2.OnNext(100);

            ts.AssertValues(2);

            dp1.OnNext(3);
            dp1.OnNext(4);

            dp2.OnNext(200);

            ts.AssertValues(2, 4);

            dp1.OnNext(5);

            dp2.OnComplete();

            ts.AssertResult(2, 4, 5);

            Assert.IsFalse(dp1.HasSubscribers);
            Assert.IsFalse(dp2.HasSubscribers);
        }
    }

}
