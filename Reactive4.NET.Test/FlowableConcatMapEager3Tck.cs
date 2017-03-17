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
    class FlowableConcatMapEager3Tck : FlowableVerification<int>
    {
        public override IPublisher<int> CreatePublisher(long elements)
        {
            int half = (int)elements / 2;
            return Flowable.ConcatEager(Flowable.Range(1, half), Flowable.Range(half, (int)elements - half));
        }
    }
}
