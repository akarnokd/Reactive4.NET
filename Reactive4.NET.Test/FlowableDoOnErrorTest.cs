using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Reactive4.NET.Test
{
    [TestFixture]
    public class FlowableDoOnErrorTest
    {
        [Test]
        public void Normal()
        {
            var seenErrors = new List<Exception>();

            Flowable.Error<int>(new InvalidOperationException())
                .DoOnError(seenErrors.Add)
                .Test();

            Assert.AreEqual(1, seenErrors.Count);
        }
        
    }
}