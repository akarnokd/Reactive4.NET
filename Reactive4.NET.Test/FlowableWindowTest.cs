using NUnit.Framework;
using System;
using System.Linq;

namespace Reactive4.NET.Test.Direct
{
    [TestFixture]
    public class FlowableWindowTest
    {
        [Test]
        public void Skip()
        {
            Flowable.Range(1, 10)
                .Window(1, 2)
                .Merge()
                .Test()
                .AssertResult(1, 3, 5, 7, 9);
        }

        [Test]
        public void Skip3()
        {
            Flowable.Range(1, 10)
                .Window(1, 3)
                .Merge()
                .Test()
                .AssertResult(1, 4, 7, 10);
        }
        
        [Test]
        public void Overlap()
        {
            Flowable.Range(1, 5)
                .Window(2, 1)
                .Merge()
                .Test()
                .AssertResult(1, 2, 2, 3, 3, 4, 4, 5, 5);
        }
    }
}
