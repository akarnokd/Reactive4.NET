using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Reactive4.NET.Test
{
    [TestFixture]
    public class FlowableBufferSizeOverlapTest
    {
        List<T> listOf<T>(params T[] items)
        {
            return new List<T>(items);
        }

        [Test]
        public void Normal()
        {
            Flowable.Range(1, 5).Buffer(2, 1)
                .Test()
                .AssertResult(
                    listOf(1, 2),
                    listOf(2, 3),
                    listOf(3, 4),
                    listOf(4, 5),
                    listOf(5)
                );
        }

        [Test]
        public void Normal3()
        {
            Flowable.Range(1, 5).Buffer(3, 1)
                .Test()
                .AssertResult(
                    listOf(1, 2, 3),
                    listOf(2, 3, 4),
                    listOf(3, 4, 5),
                    listOf(4, 5),
                    listOf(5)
                );
        }

        [Test]
        public void Normal32()
        {
            Flowable.Range(1, 5).Buffer(3, 2)
                .Test()
                .AssertResult(
                    listOf(1, 2, 3),
                    listOf(3, 4, 5),
                    listOf(5)
                );
        }

        [Test]
        public void Range2()
        {
            Flowable.Range(1, 2).Buffer(2, 1)
                .Test()
                .AssertResult(
                    listOf(1, 2),
                    listOf(2)
                );
        }

        [Test]
        public void NormalJust()
        {
            Flowable.Just(1).Buffer(2, 1)
                .Test()
                .AssertResult(
                    listOf(1)
                );
        }

        [Test]
        public void Range3()
        {
            Flowable.Range(1, 3).Buffer(2, 1)
                .RebatchRequests(1)
                .Test()
                .AssertResult(
                    listOf(1, 2),
                    listOf(2, 3),
                    listOf(3)
                );
        }
    }
}
