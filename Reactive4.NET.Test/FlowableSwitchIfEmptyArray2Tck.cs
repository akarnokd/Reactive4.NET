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
    class FlowableSwitchIfEmptyArray2Tck : FlowableVerification<int>
    {
        public override IPublisher<int> CreatePublisher(long elements)
        {
            return Flowable.Empty<int>().SwitchIfEmpty(Flowable.Empty<int>(), Flowable.Empty<int>(), Flowable.Range(1, (int)elements));
        }
    }
}
