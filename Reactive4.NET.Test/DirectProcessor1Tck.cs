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
                for (int i = 0; i < elements; i++)
                {
                    while (!dp.Offer(i)) ;
                }
                dp.OnComplete();
            }, TaskCreationOptions.LongRunning);

            return dp;
        }
    }
}
