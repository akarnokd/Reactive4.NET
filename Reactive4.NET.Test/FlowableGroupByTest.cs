using NUnit.Framework;
using System;
using System.Linq;

namespace Reactive4.NET.Test
{
    [TestFixture]
    public class FlowableGroupByTest
    {
        [Test]
        public void Normal()
        {
            Flowable.Range(1, 5).GroupBy(v => v % 2).FlatMap(v => v)
                .Test()
                .AssertResult(1, 2, 3, 4, 5);
        }

        [Test]
        public void Long()
        {
            Flowable.Range(1, 1000).GroupBy(v => v)
                .Test()
                .AssertValueCount(1000)
                .AssertNoError()
                .AssertComplete();
        }
    }
}
