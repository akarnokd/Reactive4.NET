
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
    class FlowableOnErrorResumeNext1Tck : FlowableVerification<int>
    {
        public override IPublisher<int> CreatePublisher(long elements)
        {
            return Flowable.Range(1, (int)(elements / 2))
                .ConcatWith(Flowable.Error<int>(new Exception()))
                .OnErrorResumeNext(Flowable.Range(1, (int)(elements - elements / 2)));
        }
    }
}
