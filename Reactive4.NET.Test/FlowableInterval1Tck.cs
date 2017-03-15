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
    class FlowableInterval1Tck : FlowableVerification<long>
    {
        public FlowableInterval1Tck() : base(200) { }

        public override IPublisher<long> CreatePublisher(long elements)
        {
            return Flowable.Interval(TimeSpan.FromMilliseconds(1), Executors.Task).Take(elements);
        }
    }
}
