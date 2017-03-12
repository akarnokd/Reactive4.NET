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
    [Ignore("The TCK doesn't support the behavior")]
    class UnicastProcessor2Tck_Ignored : FlowableProcessorVerification<int>
    {
        public override int CreateElement(int element)
        {
            return element;
        }

        public override IProcessor<int, int> CreateIdentityProcessor(int bufferSize)
        {
            return new UnicastProcessor<int>(bufferSize);
        }

        public override long MaxSupportedSubscribers => 1;
    }
}
