
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
    class FlowableRetry1Tck : FlowableVerification<int>
    {
        public override IPublisher<int> CreatePublisher(long elements)
        {
            int[] count = { 5 };
            return Flowable.Defer(() => {
                if (count[0]-- <= 0) {
                    return Flowable.Range(1, (int)elements);
                }
                return Flowable.Error<int>(new Exception());
            })
            .Retry();
        }
    }
}
