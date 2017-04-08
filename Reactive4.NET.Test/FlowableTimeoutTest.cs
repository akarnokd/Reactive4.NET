using NUnit.Framework;
using System;
using System.Linq;
using System.Threading;

namespace Reactive4.NET.Test
{
    [TestFixture]
    public class FlowableTimeoutTest
    {
        [Test]
        public void Normal()
        {
            Flowable.Range(1, 5).Timeout(TimeSpan.FromMinutes(5))
                .Test()
                .AssertResult(1, 2, 3, 4, 5);
        }

        [Test]
        public void Fallback()
        {
            Flowable.Never<int>().Timeout(TimeSpan.FromMilliseconds(100), Flowable.Range(1, 5))
                .Test()
                .AwaitDone(TimeSpan.FromSeconds(5))
                .AssertResult(1, 2, 3, 4, 5);
        }

        [Test]
        public void Fallback2()
        {
            Flowable.Just(0)
                .ConcatWith(Flowable.Never<int>())
                .Timeout(TimeSpan.FromMilliseconds(100), Flowable.Range(1, 5))
                .Test()
                .AwaitDone(TimeSpan.FromSeconds(5))
                .AssertResult(0, 1, 2, 3, 4, 5);
        }

        WeakReference<TestSubscriber<int>> RunLeak()
        {
            TestSubscriber<int> ts = new TestSubscriber<int>(1);
            WeakReference<TestSubscriber<int>> wr = new WeakReference<TestSubscriber<int>>(ts);

            //Flowable.Range(1, 10)
                Flowable.Never<int>().Timeout(TimeSpan.FromSeconds(100))
                .Subscribe(ts);

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
