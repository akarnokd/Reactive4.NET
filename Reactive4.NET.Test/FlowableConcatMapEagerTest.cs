using NUnit.Framework;
using System;
using System.Linq;

namespace Reactive4.NET.Test
{
    [TestFixture]
    public class FlowableConcatMapEagerTest
    {
        [Test]
        public void Normal()
        {
            Flowable.ConcatEager(Flowable.Range(1, 2), Flowable.Range(3, 3))
                .Test()
                .AssertResult(1, 2, 3, 4, 5);
        }

        [Test]
        public void NormalHidden()
        {
            Flowable.ConcatEager(Flowable.Range(1, 2).Hide(), Flowable.Range(3, 3).Hide())
                .Test()
                .AssertResult(1, 2, 3, 4, 5);
        }

        [Test]
        public void NormalAsync()
        {
            Flowable.ConcatEager(
                Flowable.Range(1, 1000).SubscribeOn(Executors.Task)
                , Flowable.Range(1001, 1000).SubscribeOn(Executors.Task))
                .Test()
                .AwaitDone(TimeSpan.FromSeconds(5))
                .AssertSubscribed()
                .AssertValueCount(2000)
                .AssertComplete()
                .AssertNoError()
                .With(ts =>
                {
                    var list = ts.Values;
                    for (int i = 1; i <= 2000; i++)
                    {
                        if (list[i - 1] != i)
                        {
                            ts.Fail("Elements differ: Expected = " + i + ", Actual = " + list[i - 1]);
                        }
                    }
                });
        }

        [Test]
        public void LessConcurrencyHidden()
        {
            var result = Flowable.Range(0, 3)
            .ConcatMapEager(x =>
            {
                return Flowable.Just(x).Hide();
            }, 2)
            .Test()
            .AssertResult(0, 1, 2);
        }

        [Test]
        public void LessConcurrency()
        {
            var result = Flowable.Range(0, 3)
            .ConcatMapEager(x =>
            {
                return Flowable.Just(x);
            }, 2)
            .Test()
            .AssertResult(0, 1, 2);
        }
    }
}
