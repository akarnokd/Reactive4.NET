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
    class FlowableCreateBufferAll1Tck : FlowableVerification<int>
    {
        public override IPublisher<int> CreatePublisher(long elements)
        {
            return Flowable.Create<int>(e =>
            {
                for (int i = 0; i < elements; i++)
                {
                    if (e.IsCancelled)
                    {
                        return;
                    }
                    e.OnNext(i);
                }
                if (!e.IsCancelled)
                {
                    e.OnComplete();
                }
            }, BackpressureStrategy.BUFFER);
        }
    }
}
