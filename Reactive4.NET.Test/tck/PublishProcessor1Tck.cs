using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Reactive.Streams;
using NUnit.Framework;
using System.Threading;

namespace Reactive4.NET.Test.Tck
{
    [TestFixture]
    class PublishProcessor1Tck : FlowableVerification<int>
    {
        public PublishProcessor1Tck() : base(50) { }

        public override IPublisher<int> CreatePublisher(long elements)
        {
            var pp = new PublishProcessor<int>();
            pp.Start();

            Task.Factory.StartNew(() => {
                while (!pp.HasSubscribers)
                {
                    Thread.Sleep(10);
                }
                for (int i = 0; i < elements; i++)
                {
                    while (!pp.Offer(i)) ;
                }
                pp.OnComplete();
            }, TaskCreationOptions.LongRunning);

            return pp;
        }
    }
}
