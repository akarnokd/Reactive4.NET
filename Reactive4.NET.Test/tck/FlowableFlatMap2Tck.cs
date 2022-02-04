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
    class FlowableFlatMap2Tck : FlowableVerification<int>
    {
        public override IPublisher<int> CreatePublisher(long elements)
        {
            if ((elements & 1) == 0)
            {
                return Flowable.Range(1, (int)elements / 2).FlatMap(v => Flowable.Range(v, 2));
            }

            return Flowable.Range(0, 1 + (int)elements / 2).FlatMap(v =>
            {
                if (v == 0)
                {
                    return Flowable.Just(v);
                }
                return Flowable.Range(v, 2);
            });
        }
    }
}
