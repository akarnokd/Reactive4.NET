using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Reactive.Streams;
using NUnit.Framework;
using System.Threading;

namespace Reactive4.NET.Test
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
    }
}
