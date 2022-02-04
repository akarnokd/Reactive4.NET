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
    class FlowableRepeatWhen1Tck : FlowableVerification<int>
    {
        public override IPublisher<int> CreatePublisher(long elements)
        {
            return Flowable.Just(1)
                .RepeatWhen(f =>
                {
                    int[] counter = { 0 };

                    return f.TakeWhile(o =>
                    {
                        return ++counter[0] < elements;
                    });
                });
        }
    }
}
