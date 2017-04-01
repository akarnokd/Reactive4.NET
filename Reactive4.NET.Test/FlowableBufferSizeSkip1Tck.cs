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
    class FlowableBufferSizeSkip1Tck : FlowableVerification<IList<int>>
    {
        public override IPublisher<IList<int>> CreatePublisher(long elements)
        {
            return Flowable.Range(1, (int)elements * 2)
                .Buffer(1, 2);
        }
    }
}
