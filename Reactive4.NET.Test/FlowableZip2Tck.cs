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
    class FlowableZip2Tck : FlowableVerification<int>
    {
        public override IPublisher<int> CreatePublisher(long elements)
        {
            return Flowable.Zip(Flowable.Range(1, (int)elements).Hide(), Flowable.Range(1, (int)elements + 1).Hide(), (a, b) => a + b);
        }
    }
}
