using NUnit.Framework;
using System;
using System.Linq;

namespace Reactive4.NET.Test
{
    [TestFixture]
    public class FlowableSwitchMapTest
    {
        [Test]
        public void Normal()
        {
            Flowable.Range(1, 5).SwitchMap(v => Flowable.Just(v))
                .Test()
                .AssertResult(1, 2, 3, 4, 5);
        }

        [Test]
        public void NormalHide()
        {
            Flowable.Range(1, 5).SwitchMap(v => Flowable.Just(v).Hide())
                .Test()
                .AssertResult(1, 2, 3, 4, 5);
        }

        [Test]
        public void Normal2()
        {
            Flowable.Just(1).SwitchMap(v => Flowable.Range(1, 5))
                .Test()
                .AssertResult(1, 2, 3, 4, 5);
        }

        [Test]
        public void Normal2Backpressure()
        {
            Flowable.Just(1).SwitchMap(v => Flowable.Range(1, 5))
                .Test(0L)
                .AssertValues()
                .RequestMore(1)
                .AssertValues(1)
                .RequestMore(2)
                .AssertValues(1, 2, 3)
                .RequestMore(2)
                .AssertResult(1, 2, 3, 4, 5);
        }

        [Test]
        public void Normal2Hide()
        {
            Flowable.Just(1).SwitchMap(v => Flowable.Range(1, 5))
                .Test()
                .AssertResult(1, 2, 3, 4, 5);
        }

        [Test]
        public void Normal2HideBackpressure()
        {
            Flowable.Just(1).SwitchMap(v => Flowable.Range(1, 5).Hide())
                .Test(0L)
                .AssertValues()
                .RequestMore(1)
                .AssertValues(1)
                .RequestMore(2)
                .AssertValues(1, 2, 3)
                .RequestMore(2)
                .AssertResult(1, 2, 3, 4, 5);
        }
    }
}
