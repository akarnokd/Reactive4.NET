using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Reactive.Streams;
using NUnit.Framework;
using System.Threading;

namespace Reactive4.NET.Test.Direct
{
    [TestFixture]
    class DirectProcessorTest
    {
        [Test]
        public void Normal()
        {
            var dp = new DirectProcessor<int>();
            Assert.IsFalse(dp.HasSubscribers);

            var ts = dp.Test();

            Assert.IsTrue(dp.HasSubscribers);

            dp.OnNext(1);
            dp.OnNext(2);
            dp.OnNext(3);
            dp.OnNext(4);

            ts.AssertValues(1, 2, 3, 4);

            dp.OnComplete();

            ts.AssertResult(1, 2, 3, 4);
        }

        [Test]
        public void Error()
        {
            var dp = new DirectProcessor<int>();
            Assert.IsFalse(dp.HasSubscribers);

            var ts = dp.Test();

            Assert.IsTrue(dp.HasSubscribers);

            dp.OnNext(1);
            dp.OnNext(2);
            dp.OnNext(3);
            dp.OnNext(4);

            ts.AssertValues(1, 2, 3, 4);

            dp.OnError(new ArgumentException("error"));

            ts.AssertFailureAndMessage(typeof(ArgumentException), "error", 1, 2, 3, 4);
        }
    }
}
