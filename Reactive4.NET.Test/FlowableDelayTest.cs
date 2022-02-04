using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Reactive4.NET.Test.Direct
{
    [TestFixture]
    class FlowableDelayTest
    {
        [Test]
        public void Normal()
        {
            Flowable.Range(1, 100)
                .Delay(TimeSpan.FromMilliseconds(1), Executors.Single)
                .Test()
                .AwaitDone(TimeSpan.FromSeconds(5))
                .AssertResult(Enumerable.Range(1, 100).ToArray());
        }


        WeakReference<TestSubscriber<int>> RunLeak()
        {
            TestSubscriber<int> ts = new TestSubscriber<int>(1);
            WeakReference<TestSubscriber<int>> wr = new WeakReference<TestSubscriber<int>>(ts);

            Flowable.Range(1, 3).Delay(TimeSpan.FromMilliseconds(1), Executors.Single)
            .Subscribe(ts);

            Thread.Sleep(25);

            ts.Cancel();

            return wr;
        }

        [Test]
        public void RefLeak()
        {

            var wr = RunLeak();

            for (int i = 0; i < 5; i++)
            {
                GC.Collect();
                Thread.Sleep(100);
            }

            var c = Executors.Computation;

            Assert.IsFalse(wr.TryGetTarget(out TestSubscriber<int> o));

            Console.WriteLine(c);
        }
    }
}
