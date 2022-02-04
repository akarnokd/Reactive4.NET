using NUnit.Framework;
using System;
using System.Linq;

namespace Reactive4.NET.Test.Direct
{
    [TestFixture]
    public class FlowableDoFinallyTest
    {
        [Test]
        public void Complete()
        {
            var run = false;

            Flowable.Range(1, 5)
                .DoFinally(() => run = true)
                .Test()
                .AssertResult(1, 2, 3, 4, 5);

            Assert.True(run);
        }

        [Test]
        public void Error()
        {
            var run = false;

            Flowable.Error<int>(new Exception())
                .DoFinally(() => run = true)
                .Test()
                .AssertFailure(typeof(Exception));

            Assert.True(run);
        }

        [Test]
        public void Cancel()
        {
            var run = false;

            Flowable.Range(1, 5)
                .DoFinally(() => run = true)
                .Take(1)
                .Test()
                .AssertResult(1);

            Assert.True(run);
        }

    }
}
