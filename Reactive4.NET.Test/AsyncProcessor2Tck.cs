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
    class AsyncProcessor2Tck : FlowableVerification<int>
    {
        public override IPublisher<int> CreatePublisher(long elements)
        {
            var pp = new AsyncProcessor<int>();

            Task.Factory.StartNew(() => {
                while (!pp.HasSubscribers)
                {
                    Thread.Sleep(10);
                }
                
                pp.OnComplete();
            }, TaskCreationOptions.LongRunning);

            return pp;
        }

        public override long MaxElementsFromPublisher => 0; 
    }
}
