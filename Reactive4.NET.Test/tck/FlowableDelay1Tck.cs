using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Reactive.Streams;
using NUnit.Framework;
using System.Threading;

namespace Reactive4.NET.Test.Tck
{
    [TestFixture]
    class FlowableDelay1Tck : FlowableVerification<int>
    {
        public FlowableDelay1Tck() : base(200) { }

        public override IPublisher<int> CreatePublisher(long elements)
        {
            return Flowable.Range(1, (int)elements).Delay(TimeSpan.FromMilliseconds(1), Executors.Single);
        }
    }
}
