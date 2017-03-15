using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reactive4.NET.Test
{
    [TestFixture]
    class FlowableDelayTest
    {
        [Test]
        public void Normal()
        {
            Flowable.Range(1, 100)
                .Delay(TimeSpan.FromMilliseconds(1), Executors.Single)
                .Test()
                .AwaitDone(TimeSpan.FromSeconds(5))
                .AssertResult(Enumerable.Range(1, 100).ToArray());
        }
    }
}
