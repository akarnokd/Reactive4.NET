using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Reactive4.NET.Test.Direct
{
    [TestFixture]
    public class FlowableParallelTest
    {
        [Test]
        public async Task ParallelSequentialBlocking()
        {
            var data = Flowable.Just(1)
            .Repeat(1024)
            .Parallel(1)
            .Sequential()
            .BlockingEnumerable();

            var list = new List<int>();

            await Task.Run(() => 
            {
                foreach (var a in data)
                {
                    list.Add(a);
                }
            });


            Assert.AreEqual(1024, list.Count);
        }

        [Test]
        public void ParallelSequential()
        {
            Flowable.Range(1, 1024)
            .Parallel(1)
            .Sequential()
            .Test()
            .AssertValueCount(1024)
            .AssertComplete()
            ;
        }

        [Test]
        public void ParallelSequentialRepeat()
        {
            Flowable.Just(1)
            .Repeat(1024)
            .Parallel(1)
            .Sequential()
            .Test()
            .AssertValueCount(1024)
            .AssertComplete()
            ;
        }

        [Test]
        public void ParallelSequentialBackpressured()
        {
            Flowable.Range(1, 1024)
            .Parallel(1)
            .Sequential()
            .Test(1024)
            .AssertValueCount(1024)
            .AssertComplete()
            ;
        }

        [Test]
        public void ParallelSequentialHidden()
        {
            Flowable.Range(1, 1024)
            .Hide()
            .Parallel(1)
            .Sequential()
            .Test()
            .AssertValueCount(1024)
            .AssertComplete()
            ;
        }
    }
}
