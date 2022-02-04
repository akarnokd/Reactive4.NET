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
    class FlowableWithLatestFromEnumerable1Tck : FlowableVerification<int>
    {
        public override IPublisher<int> CreatePublisher(long elements)
        {
            return Flowable.Range(1, (int)elements)
                .WithLatestFrom(Enumerable.Range(1, 2).Select(v => Flowable.Just(v)), (a) => a[0] + a[1] + a[2]);
        }
    }
}
