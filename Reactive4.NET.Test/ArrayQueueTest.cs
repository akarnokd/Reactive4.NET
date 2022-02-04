using NUnit.Framework;
using Reactive4.NET.utils;
using System;
using System.Linq;

namespace Reactive4.NET.Test.Direct
{
    [TestFixture]
    public class ArrayQueueTest
    {
        [Test]
        public void Normal()
        {
            var q = new ArrayQueue<int>(4);

            for (int i = 0; i < 32; i++)
            {
                Assert.IsTrue(q.Offer(1));
                Assert.IsTrue(q.Offer(2));
                Assert.IsTrue(q.Offer(3));
                Assert.IsTrue(q.Offer(4));
                Assert.IsTrue(q.Offer(5));

                Assert.IsFalse(q.IsEmpty());

                Assert.IsTrue(q.Poll(out int v));
                Assert.AreEqual(1, v);
                Assert.IsFalse(q.IsEmpty());

                Assert.IsTrue(q.Poll(out v));
                Assert.AreEqual(2, v);
                Assert.IsFalse(q.IsEmpty());

                Assert.IsTrue(q.Poll(out v));
                Assert.AreEqual(3, v);
                Assert.IsFalse(q.IsEmpty());

                Assert.IsTrue(q.Poll(out v));
                Assert.AreEqual(4, v);
                Assert.IsFalse(q.IsEmpty());

                Assert.IsTrue(q.Poll(out v));
                Assert.AreEqual(5, v);
                Assert.IsTrue(q.IsEmpty());

                Assert.IsFalse(q.Poll(out v));
            }
        }
    }
}
