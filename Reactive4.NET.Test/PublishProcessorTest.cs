using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Reactive.Streams;
using NUnit.Framework;
using System.Threading;

namespace Reactive4.NET.Test.Direct
{
    [TestFixture]
    class PublishProcessorTest
    {
        [Test]
        public void Normal()
        {
            var dp = new PublishProcessor<int>();
            dp.Start();
            Assert.IsFalse(dp.HasSubscribers);

            var ts = dp.Test();

            Assert.IsTrue(dp.HasSubscribers);

            dp.OnNext(1);
            dp.OnNext(2);
            dp.OnNext(3);
            dp.OnNext(4);

            ts.AssertValues(1, 2, 3, 4);

            dp.OnComplete();

            ts.AssertResult(1, 2, 3, 4);
        }

        [Test]
        public void Async()
        {
            var pp = new PublishProcessor<int>();
            pp.Start();

            Task.Factory.StartNew(() =>
            {
                while (!pp.HasSubscribers)
                {
                    Thread.Sleep(10);
                }
                for (int i = 0; i < 5; i++)
                {
                    while (!pp.Offer(i)) ;
                }
                pp.OnComplete();
            }, TaskCreationOptions.LongRunning);

            pp.Test().AwaitDone(TimeSpan.FromSeconds(5))
                .AssertResult(0, 1, 2, 3, 4);
        }

        [Test]
        public void Range()
        {
            Flowable.Range(1, 1000)
                .SubscribeWith(new PublishProcessor<int>())
                .Test()
                .AssertResult(Enumerable.Range(1, 1000).ToArray());
        }

        [Test]
        public void Error()
        {
            Flowable.Error<int>(new Exception())
                .SubscribeWith(new PublishProcessor<int>())
                .Test()
                .AssertFailure(typeof(Exception));
        }

        [Test]
        public void Error2()
        {
            var pp = new PublishProcessor<int>();
            pp.Start();

            var ts1 = pp.Test();
            var ts2 = pp.Test();

            pp.OnNext(1);
            pp.OnError(new Exception());

            ts1.AssertFailure(typeof(Exception), 1);
            ts2.AssertFailure(typeof(Exception), 1);
        }

        [Test]
        public void Backpressure()
        {
            var pp = new PublishProcessor<int>();
            pp.Start();

            var ts1 = pp.Test(20);

            pp.OnNext(1);
            pp.OnNext(2);

            ts1.AssertValues(1, 2);

            var ts2 = pp.Test(0);

            ts2.AssertValues();

            pp.OnNext(3);

            ts1.AssertValues(1, 2);
            ts2.AssertValues();

            ts2.Request(10);

            ts1.AssertValues(1, 2, 3);
            ts2.AssertValues(3);
        }
    }
}
