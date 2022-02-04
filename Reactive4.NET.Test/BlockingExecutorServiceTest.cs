using NUnit.Framework;
using System;
using System.Linq;

namespace Reactive4.NET.Test.Direct
{
    [TestFixture]
    public class BlockingExecutorServiceTest
    {
        [Test]
        [Timeout(5000)]
        public void Normal()
        {
            int[] run = { 0 };
            Executors.NewBlocking(s =>
            {
                s.Schedule(() =>
                {
                    run[0]++;
                    s.Shutdown();
                }, TimeSpan.FromMilliseconds(100));
                run[0]++;
            }).Start();

            Assert.AreEqual(2, run[0]);
        }
    }
}
