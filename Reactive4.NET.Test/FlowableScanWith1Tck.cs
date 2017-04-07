using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Reactive.Streams;
using NUnit.Framework;

namespace Reactive4.NET.Test
{
    [TestFixture]
    class FlowableScanWith1Tck : FlowableVerification<int>
    {
        public override IPublisher<int> CreatePublisher(long elements)
        {
            if (elements == 0)
            {
                return Flowable.Empty<int>();
            }
            return Flowable.Range(1, (int)elements - 1).Scan(() => 1, (a, b) => a + b);
        }
    }
}
