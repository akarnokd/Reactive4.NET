using NUnit.Framework;
using System;
using System.Linq;

namespace Reactive4.NET.Test.Direct
{
    [TestFixture]
    public class ReplayProcessorTest
    {
        [Test]
        public void Offline()
        {
            var rp = ReplayProcessor<int>.CreateUnbounded(2);

            for (int i = 0; i < 9; i++)
            {
                rp.OnNext(i);
            }
            rp.OnComplete();

            rp.Test()
                .AssertResult(0, 1, 2, 3, 4, 5, 6, 7, 8);
        }

        [Test]
        public void BoundedOffline()
        {
            var rp = new ReplayProcessor<int>(16);

            for (int i = 0; i < 9; i++)
            {
                rp.OnNext(i);
            }
            rp.OnComplete();

            rp.Test()
                .AssertResult(0, 1, 2, 3, 4, 5, 6, 7, 8);
        }

        [Test]
        public void BoundedTimeOffline()
        {
            var rp = new ReplayProcessor<int>(16, TimeSpan.FromMinutes(1), Executors.Task);

            for (int i = 0; i < 9; i++)
            {
                rp.OnNext(i);
            }
            rp.OnComplete();

            rp.Test()
                .AssertResult(0, 1, 2, 3, 4, 5, 6, 7, 8);
        }

        [Test]
        public void TimeOfflineNoOld()
        {
            var te = Executors.NewTest();
            var rp = new ReplayProcessor<int>(16, TimeSpan.FromSeconds(2), te);

            rp.OnNext(1);
            rp.OnNext(2);

            te.AdvanceTimeBy(TimeSpan.FromSeconds(1));

            rp.OnNext(3);
            rp.OnNext(4);

            te.AdvanceTimeBy(TimeSpan.FromSeconds(1));

            var ts = rp.Test();

            ts.AssertValues(3, 4);

            te.AdvanceTimeBy(TimeSpan.FromSeconds(1));

            rp.OnComplete();

            ts.AssertResult(3, 4);

            rp.Test().AssertResult();
        }
    }
}
