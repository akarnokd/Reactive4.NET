using NUnit.Framework;
using System;
using System.Linq;

namespace Reactive4.NET.Test
{
    [TestFixture]
    public class FlowableGroupByTest
    {
        [Test]
        public void Normal()
        {
            Flowable.Range(1, 5).GroupBy(v => v % 2).FlatMap(v => v)
                .Test()
                .AssertResult(1, 2, 3, 4, 5);
        }

        [Test]
        public void NormalHide()
        {
            Flowable.Range(1, 5).GroupBy(v => v % 2).FlatMap(v => v.Hide())
                .Test()
                .AssertResult(1, 2, 3, 4, 5);
        }

        [Test]
        public void OneGroup()
        {
            Flowable.Range(1, 5).GroupBy(v => 1).FlatMap(v => v)
                .Test()
                .AssertResult(1, 2, 3, 4, 5);
        }

        [Test]
        public void OneGroupLong()
        {
            Flowable.Range(1, 1000).GroupBy(v => 1).FlatMap(v => v)
                .Test()
                .AssertValueCount(1000)
                .AssertNoError()
                .AssertComplete();
        }


        [Test]
        public void AllGroup()
        {
            Flowable.Range(1, 5).GroupBy(v => v).FlatMap(v => v)
                .Test()
                .AssertResult(1, 2, 3, 4, 5);
        }

        [Test]
        public void AllGroupLong()
        {
            Flowable.Range(1, 1000).GroupBy(v => v).FlatMap(v => v, int.MaxValue)
                .Test()
                .AssertValueCount(1000)
                .AssertNoError()
                .AssertComplete();
        }


        [Test]
        public void AllGroupLongHidden()
        {
            Flowable.Range(1, 1000).GroupBy(v => v).FlatMap(v => v.Hide(), int.MaxValue)
                .Test()
                .AssertValueCount(1000)
                .AssertNoError()
                .AssertComplete();
        }

        [Test]
        public void Take()
        {
            Flowable.Range(1, 10).GroupBy(v => v % 3).Take(2).FlatMap(v => v)
                .Test()
                .AssertResult(1, 2, 4, 5, 7, 8, 10);
        }

        [Test]
        public void Long()
        {
            Flowable.Range(1, 1000).GroupBy(v => v)
                .DoOnNext(v => v.Subscribe())
                .Test()
                .AssertValueCount(1000)
                .AssertNoError()
                .AssertComplete();
        }

        [Test]
        public void Just()
        {
            Flowable.Just(1).GroupBy(v => v).FlatMap(v => v)
                .Test()
                .AssertResult(1);
        }

        [Test]
        public void GroupUnsubscriptionOnError()
        {
            Flowable.Range(0, 1000)
                .GroupBy(x => x % 2)
                .FlatMap(g => g.Map<int, int>(x => throw new Exception()))
                .Test()
                .AwaitDone(TimeSpan.FromSeconds(5))
                .AssertValueCount(0)
                .AssertError(typeof(Exception));
        }

        [Test]
        public void GroupUnsubscriptionOnErrorHidden()
        {
            Flowable.Range(0, 1000)
                .GroupBy(x => x % 2)
                .FlatMap(g => g.Hide().Map<int, int>(x => throw new Exception()).Hide())
                .Test()
                .AwaitDone(TimeSpan.FromSeconds(5))
                .AssertValueCount(0)
                .AssertError(typeof(Exception));
        }
    }
}
