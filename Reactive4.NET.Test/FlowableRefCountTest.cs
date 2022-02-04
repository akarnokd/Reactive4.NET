using NUnit.Framework;
using System;
using System.Linq;

namespace Reactive4.NET.Test.Direct
{
    [TestFixture]
    public class FlowableRefCountTest
    {
        [Test]
        public void Normal()
        {
            DirectProcessor<int> dp = new DirectProcessor<int>();

            var source = dp.Publish().RefCount(2);

            var ts1 = source.Test();

            Assert.IsFalse(dp.HasSubscribers);

            var ts2 = source.Test();

            Assert.IsTrue(dp.HasSubscribers, "ts2 didn't connect?");

            ts1.Cancel();

            Assert.IsTrue(dp.HasSubscribers);

            ts2.Cancel();

            Assert.IsFalse(dp.HasSubscribers);
        }

        [Test]
        public void Completing()
        {
            DirectProcessor<int> dp = new DirectProcessor<int>();

            var source = dp.Publish().RefCount(2);

            var ts3 = source.Test();

            Assert.IsFalse(dp.HasSubscribers);

            var ts4 = source.Test();

            Assert.IsTrue(dp.HasSubscribers, "ts4 didn't connect?");

            dp.OnComplete();

            ts3.AssertResult();
            ts4.AssertResult();
        }

        [Test]
        public void MultiConsume()
        {
            int[] connections = { 0 };
            var source = Flowable.Range(1, 5)
                .DoOnSubscribe(s => connections[0]++)
                .Publish().RefCount(2);

            for (int i = 1; i <= 5; i++)
            {
                var ts1 = source.Test(0);

                Assert.AreEqual(i - 1, connections[0]);

                var ts2 = source.Test(0);

                Assert.AreEqual(i, connections[0]);

                ts1.Request(5);
                ts2.Request(5);

                ts1.AssertResult(1, 2, 3, 4, 5);
                ts2.AssertResult(1, 2, 3, 4, 5);
            }
        }
    }
}
