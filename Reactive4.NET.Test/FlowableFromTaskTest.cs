using NUnit.Framework;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Reactive4.NET.Test.Direct
{
    [TestFixture]
    public class FlowableFromTaskTest
    {
        [Test]
        public void Normal()
        {
            var flowable = Task.FromResult<int>(1).ToFlowable();
            flowable.Test()
                .AwaitDone(TimeSpan.FromSeconds(5))
                .AssertResult(1);
        }

        [Test]
        public void Error()
        {
            var flowable = Task.Run(new Func<int>(() =>
            {
                throw new InvalidOperationException();
            }))
            .ToFlowable();

            var test = flowable.Test()
                .AwaitDone(TimeSpan.FromSeconds(5))
                .AssertFailure(typeof(AggregateException));
            Assert.AreSame(typeof(InvalidOperationException), test.Errors[0].InnerException.GetType());
        }

        [Test]
        public async Task Cancelled()
        {
            var flowable = DelayThrowOperationCanceledException().ToFlowable();

            try
            {
                await flowable.FirstTask(CancellationToken.None);
                Assert.IsTrue(false, "Should have thrown");
            }
            catch (OperationCanceledException ex)
            {
                // expected
                Assert.AreEqual("reason", ex.Message);
            }
        }

        async Task<int> DelayThrowOperationCanceledException()
        {
            await Task.Yield();
            throw new OperationCanceledException("reason");
        }
        [Test]
        public void Normal_Void()
        {
            var flowable = Task.Run(() =>
            {
            })
            .ToFlowable();
            flowable.Test()
                .AwaitDone(TimeSpan.FromSeconds(5))
                .AssertResult();
        }

        [Test]
        public void Error_Void()
        {
            var flowable = Task.Run(() =>
            {
                throw new InvalidOperationException();
            })
            .ToFlowable();

            var test = flowable.Test()
                .AwaitDone(TimeSpan.FromSeconds(5))
                .AssertFailure(typeof(AggregateException));
            Assert.AreSame(typeof(InvalidOperationException), test.Errors[0].InnerException.GetType());
        }

        [Test]
        public async Task Cancelled_Void()
        {
            var flowable = DelayThrowOperationCanceledException_Void().ToFlowable();

            try
            {
                await flowable.FirstTask(CancellationToken.None);
                Assert.IsTrue(false, "Should have thrown");
            }
            catch (OperationCanceledException ex)
            {
                // expected
                Assert.AreEqual("reason", ex.Message);
            }
        }

        async Task DelayThrowOperationCanceledException_Void()
        {
            await Task.Yield();
            throw new OperationCanceledException("reason");
        }
    }
}
