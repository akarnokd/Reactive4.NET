using NUnit.Framework;
using Reactive4.NET.operators;
using Reactive4.NET.utils;
using System;
using System.Linq;

namespace Reactive4.NET.Test
{
    [TestFixture]
    public class BlockingSubscribeTest
    {
        [Test]
        [Timeout(5000)]
        public void Normal()
        {
            var ts = new TestSubscriber<int>();

            Flowable.Range(1, 10)
                .BlockingSubscribe(ts);

            ts.AssertResult(1, 2, 3, 4, 5, 6, 7, 8, 9, 10);
        }

        [Test]
        [Timeout(5000)]
        public void NormalLambda()
        {
            var ts = new TestSubscriber<int>();
            ts.OnSubscribe(new BooleanSubscription());

            Flowable.Range(1, 10)
                .BlockingSubscribe(ts.OnNext, ts.OnError, ts.OnComplete);

            ts.AssertResult(1, 2, 3, 4, 5, 6, 7, 8, 9, 10);
        }

        [Test]
        [Timeout(5000)]
        public void NormalAsync()
        {
            var ts = new TestSubscriber<int>();

            Flowable.Range(1, 10)
                .Delay(TimeSpan.FromMilliseconds(10))
                .BlockingSubscribe(ts);

            ts.AssertResult(1, 2, 3, 4, 5, 6, 7, 8, 9, 10);
        }

        [Test]
        [Timeout(5000)]
        public void NormalLambdaAsync()
        {
            var ts = new TestSubscriber<int>();
            ts.OnSubscribe(new BooleanSubscription());

            Flowable.Range(1, 10)
                .Delay(TimeSpan.FromMilliseconds(10))
                .BlockingSubscribe(ts.OnNext, ts.OnError, ts.OnComplete);

            ts.AssertResult(1, 2, 3, 4, 5, 6, 7, 8, 9, 10);
        }
    }
}
