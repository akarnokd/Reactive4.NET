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
    class FlowableGenerate1Tck : FlowableVerification<int>
    {
        public override IPublisher<int> CreatePublisher(long elements)
        {
            return Flowable.Generate<int, int>(() => 1, (c, e) => {
                e.OnNext(c);
                if (c == elements)
                {
                    e.OnComplete();
                }
                return c + 1;
            });
        }
    }
}
