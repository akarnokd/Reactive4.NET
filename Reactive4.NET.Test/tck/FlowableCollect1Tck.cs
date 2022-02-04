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
    class FlowableCollect1Tck : FlowableVerification<IEnumerable<int>>
    {
        public override IPublisher<IEnumerable<int>> CreatePublisher(long elements)
        {
            return Flowable.Range(1, 1000).Collect(() => new HashSet<int>(), (a, b) => a.Add(b));
        }

        public override long MaxElementsFromPublisher => 1;
    }
}
