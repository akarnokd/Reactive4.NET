using NUnit.Framework;
using System;
using System.Linq;

namespace Reactive4.NET.Test
{
    [TestFixture]
    public class FlowableFlatMapEnumerableTest
    {
        [Test]
        public void FlatMapEnumerableTest1_Fused()
        {
            int i = 1;
            int j = 1000000 / i;
            Flowable.Range(1, i)
                .FlatMapEnumerable(v => Enumerable.Range(v, j))
                .Subscribe(new PerfFlowableSubscriber<int>());
        }

        [Test]
        public void FlatMapEnumerableTest10_Fused()
        {
            int i = 10;
            int j = 1000000 / i;
            Flowable.Range(1, i)
                .FlatMapEnumerable(v => Enumerable.Range(v, j))
                .Subscribe(new PerfFlowableSubscriber<int>());
        }

        [Test]
        public void FlatMapEnumerableTest100_Fused()
        {
            int i = 100;
            int j = 1000000 / i;
            Flowable.Range(1, i)
                .FlatMapEnumerable(v => Enumerable.Range(v, j))
                .Subscribe(new PerfFlowableSubscriber<int>());
        }

        [Test]
        public void FlatMapEnumerableTest1000_Fused()
        {
            int i = 1000;
            int j = 1000000 / i;
            Flowable.Range(1, i)
                .FlatMapEnumerable(v => Enumerable.Range(v, j))
                .Subscribe(new PerfFlowableSubscriber<int>());
        }

        [Test]
        public void FlatMapEnumerableTest10000_Fused()
        {
            int i = 10000;
            int j = 1000000 / i;
            Flowable.Range(1, i)
                .FlatMapEnumerable(v => Enumerable.Range(v, j))
                .Subscribe(new PerfFlowableSubscriber<int>());
        }

        [Test]
        public void FlatMapEnumerableTest100000_Fused()
        {
            int i = 100000;
            int j = 1000000 / i;
            Flowable.Range(1, i)
                .FlatMapEnumerable(v => Enumerable.Range(v, j))
                .Subscribe(new PerfFlowableSubscriber<int>());
        }
        [Test]
        public void FlatMapEnumerableTest1000000_Fused()
        {
            int i = 1000000;
            int j = 1000000 / i;
            Flowable.Range(1, i)
                .FlatMapEnumerable(v => Enumerable.Range(v, j))
                .Subscribe(new PerfFlowableSubscriber<int>());
        }

        [Test]
        public void FlatMapEnumerableTest1_NonFused()
        {
            int i = 1;
            int j = 1000000 / i;
            Flowable.Range(1, i)
                .Hide()
                .FlatMapEnumerable(v => Enumerable.Range(v, j))
                .Subscribe(new PerfFlowableSubscriber<int>());
        }

        [Test]
        public void FlatMapEnumerableTest10_NonFused()
        {
            int i = 10;
            int j = 1000000 / i;
            Flowable.Range(1, i)
                .Hide()
                .FlatMapEnumerable(v => Enumerable.Range(v, j))
                .Subscribe(new PerfFlowableSubscriber<int>());
        }

        [Test]
        public void FlatMapEnumerableTest100_NonFused()
        {
            int i = 100;
            int j = 1000000 / i;
            Flowable.Range(1, i)
                .Hide()
                .FlatMapEnumerable(v => Enumerable.Range(v, j))
                .Subscribe(new PerfFlowableSubscriber<int>());
        }

        [Test]
        public void FlatMapEnumerableTest1000_NonFused()
        {
            int i = 1000;
            int j = 1000000 / i;
            Flowable.Range(1, i)
                .Hide()
                .FlatMapEnumerable(v => Enumerable.Range(v, j))
                .Subscribe(new PerfFlowableSubscriber<int>());
        }

        [Test]
        public void FlatMapEnumerableTest10000_NonFused()
        {
            int i = 10000;
            int j = 1000000 / i;
            Flowable.Range(1, i)
                .Hide()
                .FlatMapEnumerable(v => Enumerable.Range(v, j))
                .Subscribe(new PerfFlowableSubscriber<int>());
        }

        [Test]
        public void FlatMapEnumerableTest100000_NonFused()
        {
            int i = 100000;
            int j = 1000000 / i;
            Flowable.Range(1, i)
                .Hide()
                .FlatMapEnumerable(v => Enumerable.Range(v, j))
                .Subscribe(new PerfFlowableSubscriber<int>());
        }
        [Test]
        public void FlatMapEnumerableTest1000000_NonFused()
        {
            int i = 1000000;
            int j = 1000000 / i;
            Flowable.Range(1, i)
                .Hide()
                .FlatMapEnumerable(v => Enumerable.Range(v, j))
                .Subscribe(new PerfFlowableSubscriber<int>());
        }

        [Test]
        public void Backpressure_NotFused()
        {
            var n = Flowable.BufferSize() + 1;
            Flowable.Range(0, n)
                .Hide()
                .FlatMapEnumerable(x => new[] { x, x + 1, x + 2 })
                .TakeLast(1)
                .Test()
                .AssertResult(n + 1);
        }

        [Test]
        public void Backpressure_NotFused_2()
        {
            var n = Flowable.BufferSize() + 1;
            Flowable.Range(0, n)
                .Hide()
                .FlatMapEnumerable(x => new[] { x, x + 1, x + 2 }, 1)
                .TakeLast(1)
                .Test()
                .AssertResult(n + 1);
        }

        [Test]
        public void FlattenArray()
        {
            var n = 1000;
            Flowable.Range(0, n)
                .FlatMapEnumerable(x => new[] { x })
                .Test()
                .AssertValueCount(n)
                .AssertNoError()
                .AssertComplete();
        }

        [Test]
        public void FlattenArray_NotFused()
        {
            var n = 1000;
            Flowable.Range(0, n)
                .Hide()
                .FlatMapEnumerable(x => new[] { x })
                .Test()
                .AssertValueCount(n)
                .AssertNoError()
                .AssertComplete();
        }
    }
}
