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
    class FlowableTimerTck : FlowableVerification<long>
    {
        public FlowableTimerTck() : base(100) { }

        public override IPublisher<long> CreatePublisher(long elements)
        {
            return Flowable.Timer(TimeSpan.FromMilliseconds(1), Executors.Task);
        }

        public override long MaxElementsFromPublisher => 1;
    }
}
