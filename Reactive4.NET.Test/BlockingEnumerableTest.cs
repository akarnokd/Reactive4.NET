using NUnit.Framework;
using System;
using System.Linq;

namespace Reactive4.NET.Test.Direct
{
    [TestFixture]
    public class BlockingEnumerableTest
    {
        [Test]
        public void Normal()
        {
            int c = 0;

            var scr = Flowable.Range(1, 1000);//.ObserveOn(Executors.Single);
            var en = scr.BlockingEnumerable();
            foreach (int v in en)
            {
                c++;
            }

            Assert.AreEqual(1000, c);

            c = 0;
            foreach (int v in en)
            {
                c++;
            }
        }

        [Test]
        public void NormalAsync()
        {
            int c = 0;

            var scr = Flowable.Range(1, 1000).ObserveOn(Executors.Task);

            foreach (int v in scr.BlockingEnumerable())
            {
                c++;
            }

            Assert.AreEqual(1000, c);
        }
    }
}
