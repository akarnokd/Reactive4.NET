using NUnit.Framework;
using System;
using System.Linq;

namespace Reactive4.NET.Test
{
    [TestFixture]
    public class FlowableDistinctTest
    {
        [Test]
        public void Normal()
        {
            Flowable.FromArray(1, 2, 2, 3, 3, 4, 4, 5, 5, 6)
                .Distinct()
                .Test()
                .AssertResult(1, 2, 3, 4, 5, 6);
        }

        [Test]
        public void NormalConditional()
        {
            Flowable.FromArray(1, 2, 2, 3, 3, 4, 4, 5, 5, 6)
                .Distinct()
                .Filter(v => true)
                .Test()
                .AssertResult(1, 2, 3, 4, 5, 6);
        }
    }
}
