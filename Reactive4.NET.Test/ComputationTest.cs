using NUnit.Framework;
using System;
using System.Linq;
using System.Threading;

namespace Reactive4.NET.Test.Direct
{
    [TestFixture]
    public class ComputationTest
    {
        [Test]
        public void Direct()
        {
            var t = Executors.Computation.Now;
            Executors.Computation.Schedule(() => Console.WriteLine(t - Executors.Computation.Now), TimeSpan.FromSeconds(2));

            Thread.Sleep(3000);
        }

        [Test]
        public void Worker()
        {
            var t = Executors.Computation.Now;
            var w = Executors.Computation.Worker;
            try
            {
                w.Schedule(() => Console.WriteLine(t - Executors.Computation.Now), TimeSpan.FromSeconds(2));
                Thread.Sleep(3000);
            }
            finally
            {
                w.Dispose();
            }
        }
    }
}
