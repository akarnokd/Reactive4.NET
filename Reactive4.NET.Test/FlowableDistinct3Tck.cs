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
    class FlowableDistinct3Tck : FlowableVerification<int>
    {
        public override IPublisher<int> CreatePublisher(long elements)
        {
            return Flowable.Range(1, (int)elements)
                .FlatMap(v => v == 1 ? Flowable.Just(v) : Flowable.FromArray(v, v - 1))
                .Distinct()
                .Filter(v => true);
        }
    }
}
