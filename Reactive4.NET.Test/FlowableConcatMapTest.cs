using NUnit.Framework;
using System;
using System.Linq;

namespace Reactive4.NET.Test.Direct
{
    [TestFixture]
    public class FlowableConcatMapTest
    {
        [Test]
        public void Normal()
        {
            Flowable.Range(1, 5).ConcatMap(v => Flowable.Just(v + 1))
                .Test()
                .AssertResult(2, 3, 4, 5, 6);
        }

        [Test]
        public void NormalImmediate()
        {
            Flowable.Range(1, 5).ConcatMap(v => Flowable.Just(v + 1), ErrorMode.Immediate)
                .Test()
                .AssertResult(2, 3, 4, 5, 6);
        }

        [Test]
        public void NormalHide()
        {
            Flowable.Range(1, 5).Hide().ConcatMap(v => Flowable.Just(v + 1))
                .Test()
                .AssertResult(2, 3, 4, 5, 6);
        }

        [Test]
        public void NormalHideImmediate()
        {
            Flowable.Range(1, 5).Hide().ConcatMap(v => Flowable.Just(v + 1), ErrorMode.Immediate)
                .Test()
                .AssertResult(2, 3, 4, 5, 6);
        }

        [Test]
        public void NormalBackpressured()
        {
            Flowable.Range(1, 5).ConcatMap(v => Flowable.Just(v + 1))
                .Test(0L)
                .AssertValues()
                .RequestMore(1)
                .AssertValues(2)
                .RequestMore(2)
                .AssertValues(2, 3, 4)
                .RequestMore(2)
                .AssertResult(2, 3, 4, 5, 6);
        }

        [Test]
        public void NormalImmediateBackpressured()
        {
            Flowable.Range(1, 5).ConcatMap(v => Flowable.Just(v + 1), ErrorMode.Immediate)
                .Test(0L)
                .AssertValues()
                .RequestMore(1)
                .AssertValues(2)
                .RequestMore(2)
                .AssertValues(2, 3, 4)
                .RequestMore(2)
                .AssertResult(2, 3, 4, 5, 6);
        }

        [Test]
        public void DefaultMainError()
        {
            Flowable.Range(1, 5)
                .ConcatWith(Flowable.Error<int>(new ArgumentException("error")))
                .ConcatMap(v => Flowable.Just(v + 1))
                .Test()
                .AssertFailureAndMessage(typeof(ArgumentException), "error", 2, 3, 4, 5, 6);
        }

        [Test]
        public void DefaultInnerError()
        {
            Flowable.Range(1, 5)
                .ConcatMap(v => { 
                    if (v == 3)
                    {
                        return Flowable.Error<int>(new ArgumentException("error"));
                    }
                    return Flowable.Just(v + 1); 
                })
                .Test()
                .AssertFailureAndMessage(typeof(ArgumentException), "error", 2, 3, 5, 6);
        }

        [Test]
        public void ImmediateErrorOuter()
        {
            DirectProcessor<int> outer = new DirectProcessor<int>();
            DirectProcessor<int> inner = new DirectProcessor<int>();

            var ts = outer.ConcatMap(v => inner, ErrorMode.Immediate)
                .Test();

            Assert.True(outer.HasSubscribers);
            Assert.False(inner.HasSubscribers);

            outer.OnNext(1);

            Assert.True(inner.HasSubscribers);

            outer.OnError(new ArgumentException("error"));

            Assert.False(outer.HasSubscribers);
            Assert.False(inner.HasSubscribers);

            ts.AssertFailureAndMessage(typeof(ArgumentException), "error");
        }

        [Test]
        public void ImmediateErrorInner()
        {
            DirectProcessor<int> outer = new DirectProcessor<int>();
            DirectProcessor<int> inner = new DirectProcessor<int>();

            var ts = outer.ConcatMap(v => inner, ErrorMode.Immediate)
                .Test();

            Assert.True(outer.HasSubscribers);
            Assert.False(inner.HasSubscribers);

            outer.OnNext(1);

            Assert.True(inner.HasSubscribers);

            inner.OnError(new ArgumentException("error"));

            Assert.False(outer.HasSubscribers);
            Assert.False(inner.HasSubscribers);

            ts.AssertFailureAndMessage(typeof(ArgumentException), "error");
        }

        [Test]
        public void BoundaryErrorOuter()
        {
            DirectProcessor<int> outer = new DirectProcessor<int>();
            DirectProcessor<int> inner = new DirectProcessor<int>();

            var ts = outer.ConcatMap(v => inner, ErrorMode.Boundary)
                .Test();

            Assert.True(outer.HasSubscribers);
            Assert.False(inner.HasSubscribers);

            outer.OnNext(1);

            Assert.True(inner.HasSubscribers);

            outer.OnError(new ArgumentException("error"));

            Assert.False(outer.HasSubscribers);
            Assert.True(inner.HasSubscribers);

            inner.OnNext(1);
            inner.OnComplete();

            ts.AssertFailureAndMessage(typeof(ArgumentException), "error", 1);
        }

        [Test]
        public void BoundaryErrorInner()
        {
            DirectProcessor<int> outer = new DirectProcessor<int>();
            DirectProcessor<int> inner = new DirectProcessor<int>();

            var ts = outer.ConcatMap(v => inner, ErrorMode.Boundary)
                .Test();

            Assert.True(outer.HasSubscribers);
            Assert.False(inner.HasSubscribers);

            outer.OnNext(1);

            Assert.True(inner.HasSubscribers);

            inner.OnNext(1);
            inner.OnError(new ArgumentException("error"));

            Assert.False(outer.HasSubscribers);
            Assert.False(inner.HasSubscribers);

            ts.AssertFailureAndMessage(typeof(ArgumentException), "error", 1);
        }
    }
}
