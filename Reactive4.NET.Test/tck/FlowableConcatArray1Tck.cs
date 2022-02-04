using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Reactive.Streams;
using NUnit.Framework;

namespace Reactive4.NET.Test.Tck
{
    [TestFixture]
    class FlowableConcatArray1Tck : FlowableVerification<int>
    {
        public override IPublisher<int> CreatePublisher(long elements)
        {
            long half = elements / 2;
            return Flowable.Concat(Flowable.Range(0, (int)half), Flowable.Range((int)half, (int)(elements - half)));
        }
    }
}
