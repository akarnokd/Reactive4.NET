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
    class FlowableConcatMap3Tck : FlowableVerification<int>
    {
        public override IPublisher<int> CreatePublisher(long elements)
        {
            return Flowable.Range(1, 2).ConcatMap(v =>
            {
                int half = (int)elements / 2;
                if (v == 1)
                {
                    return Flowable.Range(1, half);
                }
                return Flowable.Range(1 + half, (int)elements - half);
            });
        }
    }
}
