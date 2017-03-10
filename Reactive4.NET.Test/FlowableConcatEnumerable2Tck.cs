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
    class FlowableConcatEnumerable2Tck : FlowableVerification<int>
    {
        IEnumerable<R> FromArray<R>(params R[] array)
        {
            return array;
        }

        public override IPublisher<int> CreatePublisher(long elements)
        {
            long half = elements / 2;
            return Flowable.Concat(FromArray(Flowable.Range(0, (int)half), Flowable.Range((int)half, (int)(elements - half)))).Filter(v => true);
        }
    }
}
