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
    class FlowableFromTask2Tck : FlowableVerification<object>
    {
        public override IPublisher<object> CreatePublisher(long elements)
        {
            return Flowable.FromTask(Task.Run(() => { }));
        }

        public override long MaxElementsFromPublisher => 0;
    }
}
