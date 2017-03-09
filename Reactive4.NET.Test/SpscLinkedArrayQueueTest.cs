using NUnit.Framework;
using System;
using System.Linq;
using Reactive4.NET.utils;

namespace Reactive4.NET.Test
{
    [TestFixture]
    public class SpscLinkedArrayQueueTest
    {
        [Test]
        public void Normal()
        {
            var q = new SpscLinkedArrayQueue<int>(2);
            q.Offer(1);
            q.Offer(2);
            q.Offer(3);

            Assert.IsTrue(q.Poll(out int v1));
            Assert.AreEqual(1, v1);

            Assert.IsTrue(q.Poll(out int v2));
            Assert.AreEqual(2, v2);

            Assert.IsTrue(q.Poll(out int v3));
            Assert.AreEqual(3, v3);

            Assert.IsFalse(q.Poll(out int v4));
            Assert.IsTrue(q.IsEmpty());
        }

        [Test]
        public void Normal2()
        {
            var q = new SpscLinkedArrayQueue<int>(16);
            q.Offer(1);
            q.Offer(2);
            q.Offer(3);

            Assert.IsTrue(q.Poll(out int v1));
            Assert.AreEqual(1, v1);

            Assert.IsTrue(q.Poll(out int v2));
            Assert.AreEqual(2, v2);

            Assert.IsTrue(q.Poll(out int v3));
            Assert.AreEqual(3, v3);

            Assert.IsFalse(q.Poll(out int v4));
            Assert.IsTrue(q.IsEmpty());
        }
    }
}
