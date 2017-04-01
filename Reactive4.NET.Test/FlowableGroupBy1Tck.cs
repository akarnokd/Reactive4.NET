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
    class FlowableGroupBy1Tck : FlowableVerification<IGroupedFlowable<int, int>>
    {
        public override IPublisher<IGroupedFlowable<int, int>> CreatePublisher(long elements)
        {
            return Flowable.Range(1, (int)elements)
                .GroupBy(v => v)
                .DoOnNext(v => v.Subscribe());
        }
    }
}
