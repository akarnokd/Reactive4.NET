using NUnit.Framework;
using System;
using System.Linq;

namespace Reactive4.NET.Test
{
    [TestFixture]
    public class FlowableZipTest
    {
        [Test]
        public void Normal1()
        {
            Flowable.Zip(Flowable.Range(1, 2), Flowable.Range(1, 2), (a, b) => a + b)
                .Test()
                .AssertResult(2, 4);
        }

        [Test]
        public void Normal2()
        {
            Flowable.Zip(Flowable.Range(1, 2), Flowable.Range(1, 3), (a, b) => a + b)
                .Test()
                .AssertResult(2, 4);
        }

        [Test]
        public void Normal3()
        {
            Flowable.Zip(Flowable.Range(1, 3), Flowable.Range(1, 2), (a, b) => a + b)
                .Test()
                .AssertResult(2, 4);
        }

        [Test]
        public void Normal4()
        {
            Flowable.Zip(Flowable.Empty<int>(), Flowable.Range(1, 2), (a, b) => a + b)
                .Test()
                .AssertResult();
        }

        [Test]
        public void Normal5()
        {
            Flowable.Zip(Flowable.Range(1, 2), Flowable.Empty<int>(), (a, b) => a + b)
                .Test()
                .AssertResult();
        }

        [Test]
        public void Normal1Hide()
        {
            Flowable.Zip(Flowable.Range(1, 2).Hide(), Flowable.Range(1, 2).Hide(), (a, b) => a + b)
                .Test()
                .AssertResult(2, 4);
        }

        [Test]
        public void Normal2Hide()
        {
            Flowable.Zip(Flowable.Range(1, 2).Hide(), Flowable.Range(1, 3).Hide(), (a, b) => a + b)
                .Test()
                .AssertResult(2, 4);
        }

        [Test]
        public void Normal3Hide()
        {
            Flowable.Zip(Flowable.Range(1, 3).Hide(), Flowable.Range(1, 2).Hide(), (a, b) => a + b)
                .Test()
                .AssertResult(2, 4);
        }

        [Test]
        public void Normal4Hide()
        {
            Flowable.Zip(Flowable.Empty<int>().Hide(), Flowable.Range(1, 2).Hide(), (a, b) => a + b)
                .Test()
                .AssertResult();
        }

        [Test]
        public void Normal5Hide()
        {
            Flowable.Zip(Flowable.Range(1, 2).Hide(), Flowable.Empty<int>().Hide(), (a, b) => a + b)
                .Test()
                .AssertResult();
        }
    }
}
