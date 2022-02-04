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
    [Ignore("The TCK doesn't support this behavior")]
    class AsyncProcessor4Tck_Ignored : FlowableProcessorVerification<object>
    {

        public override long MaxElementsFromPublisher => 1;

        public override long MaxSupportedSubscribers => 1; 

        public override object CreateElement(int element)
        {
            return element;
        }

        public override IProcessor<object, object> CreateIdentityProcessor(int bufferSize)
        {
            return new AsyncProcessor<object>().RefCount();
        }
    }
}
