using NUnit.Framework;
using System;
using System.Linq;

namespace Reactive4.NET.Test.Direct
{
    [TestFixture]
    public class FlowableGenerateTest
    {
        [Test]
        public void Backpressured()
        {
            Flowable.Generate<int, int>(() => 1, (s, e) =>
            {
                e.OnNext(s);
                if (s == 5)
                {
                    e.OnComplete();
                }
                return s + 1;
            })
            .Test(0)
            .AssertValues()
            .RequestMore(1)
            .AssertValues(1)
            .RequestMore(2)
            .AssertValues(1, 2, 3)
            .RequestMore(2)
            .AssertResult(1, 2, 3, 4, 5)
            ;
        }
    }
}
