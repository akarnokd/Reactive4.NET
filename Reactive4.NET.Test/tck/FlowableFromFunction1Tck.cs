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
    class FlowableFromFunction1Tck : FlowableVerification<int>
    {
        public override IPublisher<int> CreatePublisher(long elements)
        {
            return Flowable.FromFunction(() => (int)elements);
        }

        public override long MaxElementsFromPublisher => 1;
    }
}
