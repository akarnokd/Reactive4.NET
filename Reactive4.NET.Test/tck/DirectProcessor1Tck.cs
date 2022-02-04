using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Reactive.Streams;
using NUnit.Framework;
using System.Threading;
using Reactive4.NET.utils;

namespace Reactive4.NET.Test.Tck
{
    [TestFixture]
    class DirectProcessor1Tck : FlowableVerification<int>
    {
        public DirectProcessor1Tck() : base(50) { }

        public override IPublisher<int> CreatePublisher(long elements)
        {
            var dp = new DirectProcessor<int>();

            Task.Factory.StartNew(() => {
                while (!dp.HasSubscribers)
                {
                    Thread.Sleep(10);
                }
                long start = SchedulerHelper.NowUTC();
                for (int i = 0; i < elements; i++)
                {
                    while (!dp.Offer(i))
                    {
                        Thread.Sleep(1);
                        if (SchedulerHelper.NowUTC() - start > 1000)
                        {
                            return;
                        }
                    }
                }
                dp.OnComplete();
            }, TaskCreationOptions.LongRunning);

            return dp;
        }
    }
}
