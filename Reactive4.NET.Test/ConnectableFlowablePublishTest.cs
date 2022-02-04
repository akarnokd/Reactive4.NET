using NUnit.Framework;
using System;
using System.Linq;

namespace Reactive4.NET.Test.Direct
{
    [TestFixture]
    public class ConnectableFlowablePublishTest
    {
        [Test]
        public void Normal()
        {
            var source = Flowable.Range(1, 10)
                .Publish(4)
                ;

            var ts1 = source.Test(0);
            var ts2 = source.Test(0);

            Assert.AreEqual(ConnectionState.Fresh, source.ConnectionState);

            source.Connect();

            Assert.AreEqual(ConnectionState.Connected, source.ConnectionState);

            ts1.Request(10);
            ts2.Request(10);

            ts1.AssertResult(1, 2, 3, 4, 5, 6, 7, 8, 9, 10);
            ts2.AssertResult(1, 2, 3, 4, 5, 6, 7, 8, 9, 10);

            Assert.AreEqual(ConnectionState.Terminated, source.ConnectionState);

            source.Test().AssertResult();

            source.Reset();

            Assert.AreEqual(ConnectionState.Fresh, source.ConnectionState);

            var ts3 = source.Test();

            source.Connect();

            ts3.AssertResult(1, 2, 3, 4, 5, 6, 7, 8, 9, 10);

            Assert.AreEqual(ConnectionState.Terminated, source.ConnectionState);

            source.Connect();

            Assert.AreEqual(ConnectionState.Connected, source.ConnectionState);

            source.Test().AssertResult(1, 2, 3, 4, 5, 6, 7, 8, 9, 10);

            Assert.AreEqual(ConnectionState.Terminated, source.ConnectionState);
        }
    }
}
