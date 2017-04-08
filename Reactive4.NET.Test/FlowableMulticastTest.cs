using NUnit.Framework;
using System;
using System.Linq;

namespace Reactive4.NET.Test
{
    [TestFixture]
    public class FlowableMulticastTest
    {
        [Test]
        public void Publish()
        {
            Flowable.Range(1, 5)
                .Publish(f => f.Take(3).ConcatWith(f.TakeLast(3)))
                .Test()
                .AssertResult(1, 2, 3, 4, 5);
        }

        [Test]
        public void PublishHide()
        {
            Flowable.Range(1, 5).Hide()
                .Publish(f => f.Take(3).ConcatWith(f.TakeLast(3)))
                .Test()
                .AssertResult(1, 2, 3, 4, 5);
        }

        [Test]
        public void Replay()
        {
            Flowable.Range(1, 5)
                .Replay(f => f.Take(3).ConcatWith(f.TakeLast(3)))
                .Test()
                .AssertResult(1, 2, 3, 3, 4, 5);
        }

        [Test]
        public void ReplayHide()
        {
            Flowable.Range(1, 5)
                .Replay(f => f.Take(3).ConcatWith(f.TakeLast(3)))
                .Test()
                .AssertResult(1, 2, 3, 3, 4, 5);
        }
        [Test]
        public void Disconnect()
        {
            var pp = new PublishProcessor<int>();

            pp.Publish(f => Flowable.Range(1, 5))
                .Test()
                .AssertResult(1, 2, 3, 4, 5);

            Assert.IsFalse(pp.HasSubscribers);
        }
    }
}
