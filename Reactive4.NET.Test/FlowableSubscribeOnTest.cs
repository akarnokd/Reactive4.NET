using NUnit.Framework;
using System;
using System.Linq;

namespace Reactive4.NET.Test.Direct
{
    [TestFixture]
    public class FlowableSubscribeOnTest
    {
        [Test]
        public void Simple()
        {
            Flowable.Range(1, 5)
                .SubscribeOn(Executors.Task)
                .Test()
                .AwaitDone(TimeSpan.FromSeconds(5))
                .AssertResult(1, 2, 3, 4, 5);
        }
    }
}
