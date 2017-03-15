using NUnit.Framework;
using System;
using System.Linq;

namespace Reactive4.NET.Test
{
    [TestFixture]
    public class FlowableDistinctUntilChangedTest
    {
        [Test]
        public void Normal()
        {
            Flowable.FromArray(1, 2, 2, 3, 3, 4, 4, 5, 5, 6)
                .DistinctUntilChanged()
                .Test()
                .AssertResult(1, 2, 3, 4, 5, 6);
        }

        [Test]
        public void NormalConditional()
        {
            Flowable.FromArray(1, 2, 2, 3, 3, 4, 4, 5, 5, 6)
                .DistinctUntilChanged()
                .Filter(v => true)
                .Test()
                .AssertResult(1, 2, 3, 4, 5, 6);
        }
    }
}
