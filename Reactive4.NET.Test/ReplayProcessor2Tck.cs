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
    class ReplayProcessor2Tck : FlowableVerification<int>
    {
        public override IPublisher<int> CreatePublisher(long elements)
        {
            var pp = new ReplayProcessor<int>((int)elements + 10);

            Task.Factory.StartNew(() => {
                while (!pp.HasSubscribers)
                {
                    Thread.Sleep(1);
                }
                for (int i = 0; i < elements; i++)
                {
                    pp.OnNext(i);
                }
                pp.OnComplete();
            }, TaskCreationOptions.LongRunning);

            return pp;
        }
    }
}
