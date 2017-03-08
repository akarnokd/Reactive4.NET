using NUnit.Framework;
using System;
using System.Linq;

namespace Reactive4.NET.Test
{
    [TestFixture]
    public class FlowableFlatMapTest
    {
        [Test]
        public void Simple()
        {
            Flowable.Range(1, 5)
                .FlatMap(v => Flowable.Range(v, 2))
                .Test()
                .AssertResult(1, 2, 2, 3, 3, 4, 4, 5, 5, 6);
        }
    }
}
