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
    class FlowableConcatMapEager4Tck : FlowableVerification<int>
    {
        public override IPublisher<int> CreatePublisher(long elements)
        {
            int half = (int)elements / 2;
            return Flowable.ConcatEager(Flowable.Range(1, half).Hide(), Flowable.Range(half, (int)elements - half).Hide());
        }
    }
}
