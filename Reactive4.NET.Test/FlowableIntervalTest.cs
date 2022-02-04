using NUnit.Framework;
using System;
using System.Linq;

namespace Reactive4.NET.Test.Direct
{
    [TestFixture]
    public class FlowableIntervalTest
    {
        [Test]
        public void Normal()
        {
            var ts = Flowable.Interval(TimeSpan.FromMilliseconds(10))
                .Take(10)
                .Test()
                ;

                ts.AwaitDone(TimeSpan.FromSeconds(500))
                .AssertResult(0L, 1L, 2L, 3L, 4L, 5L, 6L, 7L, 8L, 9L);
        }
    }
}
