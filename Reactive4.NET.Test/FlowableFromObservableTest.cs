using NUnit.Framework;
using System;
using System.Linq;

namespace Reactive4.NET.Test.Direct
{
    [TestFixture]
    public class FlowableFromObservableTest
    {
        [Test]
        public void Buffer()
        {
            Flowable.Range(1, 5)
                .ToObservable()
                .ToFlowable(BackpressureStrategy.BUFFER)
                .Test()
                .AssertResult(1, 2, 3, 4, 5);
        }
        [Test]
        public void BufferBackpressured()
        {
            Flowable.Range(1, 5)
                .ToObservable()
                .ToFlowable(BackpressureStrategy.BUFFER)
                .Test(0L)
                .AssertEmpty()
                .RequestMore(5)
                .AssertResult(1, 2, 3, 4, 5);
        }
    }
}
