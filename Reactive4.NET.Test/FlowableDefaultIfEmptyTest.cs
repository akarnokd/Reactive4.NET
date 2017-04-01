using NUnit.Framework;
using System;
using System.Linq;

namespace Reactive4.NET.Test
{
    [TestFixture]
    public class FlowableDefaultIfEmptyTest
    {
        [Test]
        public void Empty()
        {
            Flowable.Empty<int>()
                .DefaultIfEmpty(1)
                .Test()
                .AssertResult(1);
        }

        [Test]
        public void EmptyBackpressured()
        {
            Flowable.Empty<int>()
                .DefaultIfEmpty(1)
                .Test(0)
                .AssertEmpty()
                .RequestMore(1)
                .AssertResult(1);
        }
    }
}
