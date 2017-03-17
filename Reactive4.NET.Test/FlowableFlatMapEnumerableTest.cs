using NUnit.Framework;
using System;
using System.Linq;

namespace Reactive4.NET.Test
{
    [TestFixture]
    public class FlowableFlatMapEnumerableTest
    {
        [Test]
        public void FlatMapEnumerableTest1()
        {
            int i = 1;
            int j = 1000000 / i;
            Flowable.Range(1, i)
                .FlatMapEnumerable(v => Enumerable.Range(v, j))
                .Subscribe(new PerfFlowableSubscriber<int>());
        }

        [Test]
        public void FlatMapEnumerableTest10()
        {
            int i = 10;
            int j = 1000000 / i;
            Flowable.Range(1, i)
                .FlatMapEnumerable(v => Enumerable.Range(v, j))
                .Subscribe(new PerfFlowableSubscriber<int>());
        }

        [Test]
        public void FlatMapEnumerableTest100()
        {
            int i = 100;
            int j = 1000000 / i;
            Flowable.Range(1, i)
                .FlatMapEnumerable(v => Enumerable.Range(v, j))
                .Subscribe(new PerfFlowableSubscriber<int>());
        }

        [Test]
        public void FlatMapEnumerableTest1000()
        {
            int i = 1000;
            int j = 1000000 / i;
            Flowable.Range(1, i)
                .FlatMapEnumerable(v => Enumerable.Range(v, j))
                .Subscribe(new PerfFlowableSubscriber<int>());
        }

        [Test]
        public void FlatMapEnumerableTest10000()
        {
            int i = 10000;
            int j = 1000000 / i;
            Flowable.Range(1, i)
                .FlatMapEnumerable(v => Enumerable.Range(v, j))
                .Subscribe(new PerfFlowableSubscriber<int>());
        }

        [Test]
        public void FlatMapEnumerableTest100000()
        {
            int i = 100000;
            int j = 1000000 / i;
            Flowable.Range(1, i)
                .FlatMapEnumerable(v => Enumerable.Range(v, j))
                .Subscribe(new PerfFlowableSubscriber<int>());
        }
        [Test]
        public void FlatMapEnumerableTest1000000()
        {
            int i = 1000000;
            int j = 1000000 / i;
            Flowable.Range(1, i)
                .FlatMapEnumerable(v => Enumerable.Range(v, j))
                .Subscribe(new PerfFlowableSubscriber<int>());
        }
    }
}
