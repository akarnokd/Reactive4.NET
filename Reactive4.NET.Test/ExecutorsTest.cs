using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Reactive4.NET.Test
{
    [TestFixture]
    class ExecutorsTest
    {
        [Test]
        public void periodic()
        {
            IDisposable d = NET.Executors.Task.Schedule(() => Console.WriteLine(DateTime.Now.ToString("ss.fff")), TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(100));

            Thread.Sleep(1000);

            d.Dispose();
        }

        [Test]
        public void periodicWorker()
        {
            IExecutorService task = NET.Executors.Task;
            using (var w = task.Worker)
            {
                IDisposable d = w.Schedule(() => Console.WriteLine(DateTime.Now.ToString("ss.fff")), TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(100));

                Thread.Sleep(1000);
            }
        }

        [Test]
        public void test()
        {
            TestExecutor exec = NET.Executors.NewTest();

            for (int i = 0; i < 10; i++)
            {
                exec.Schedule(() => Console.WriteLine(exec.Now), TimeSpan.FromSeconds(i));
            }

            exec.AdvanceTimeBy(TimeSpan.FromSeconds(10));
        }

        [Test]
        public void testPeriodic()
        {
            TestExecutor exec = NET.Executors.NewTest();

            exec.Schedule(() => Console.WriteLine(exec.Now), TimeSpan.FromSeconds(0), TimeSpan.FromSeconds(1));

            exec.AdvanceTimeBy(TimeSpan.FromSeconds(10));
        }
    }
}
