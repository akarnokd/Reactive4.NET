
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
    class FlowableOnErrorReturn1Tck : FlowableVerification<int>
    {
        public override IPublisher<int> CreatePublisher(long elements)
        {
            return Flowable.Range(1, (int)(elements - 1))
                .ConcatWith(Flowable.Error<int>(new Exception()))
                .OnErrorReturn((int)elements);
        }
    }
}
