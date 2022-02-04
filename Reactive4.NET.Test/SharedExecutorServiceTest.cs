using NUnit.Framework;
using System;
using System.Linq;

namespace Reactive4.NET.Test.Direct
{
    [TestFixture]
    public class SharedExecutorServiceTest
    {
        [Test]
        public void Single()
        {
            for (int i = 0; i < 100; i++)
            {
                Flowable.Just(1).SubscribeOn(Executors.Single)
                                    .Test()
                                    .WithTag("" + i)
                                    .AwaitDone(TimeSpan.FromSeconds(5))
                                    .AssertResult(1);
            }
        }

        [Test]
        public void SingleShared()
        {
            var w = Executors.Single.Worker;
            var sw = Executors.NewShared(w);

            try
            {
                Flowable.Just(1).SubscribeOn(Executors.Single)
                                    .Test()
                                    .WithTag("NonShared - 1")
                                    .AwaitDone(TimeSpan.FromSeconds(5))
                                    .AssertResult(1);

                Flowable.Just(1).SubscribeOn(sw)
                                .Test()
                                .WithTag("First")
                                .AwaitDone(TimeSpan.FromSeconds(5))
                                .AssertResult(1);

                Flowable.Just(1).SubscribeOn(Executors.Single)
                                    .Test()
                                    .WithTag("NonShared - 2")
                                    .AwaitDone(TimeSpan.FromSeconds(5))
                                    .AssertResult(1);
                
                Flowable.Just(1).SubscribeOn(Executors.NewShared(w))
                                .Test()
                                .WithTag("Second")
                                .AwaitDone(TimeSpan.FromSeconds(5))
                                .AssertResult(1);
            }
            finally
            {
                w.Dispose();
            }
        }

        [Test]
        public void Shared()
        {
            IExecutorService[] execs = { Executors.Single, Executors.Computation, Executors.IO };
            foreach (var exec in execs)
            {
                for (int i = 0; i < 10; i++)
                {
                    for (int j = 0; j < 10; j++)
                    {
                        Flowable.Just(1).SubscribeOn(exec)
                            .Test()
                            .WithTag("SubscribeOn round " + i + "/" + j + " - " + exec.GetType().Name)
                            .AwaitDone(TimeSpan.FromSeconds(5))
                            .AssertResult(1);

                        Flowable.Just(1).Delay(TimeSpan.FromMilliseconds(10))
                            .Test()
                            .WithTag("Delay round " + i + "/" + j + " - " + exec.GetType().Name)
                            .AwaitDone(TimeSpan.FromSeconds(5))
                            .AssertResult(1);

                        Flowable.Interval(TimeSpan.FromMilliseconds(10))
                            .Take(5)
                            .Test()
                            .WithTag("Interval round " + i + "/" + j + " - " + exec.GetType().Name)
                            .AwaitDone(TimeSpan.FromSeconds(5))
                            .AssertResult(0L, 1L, 2L, 3L, 4L);
                    }
                }
            }
        }

        [Test]
        public void AsyncShared()
        {
            IExecutorService[] execs = { Executors.Single, Executors.Computation, Executors.IO };
            foreach (var exec in execs)
            {
                for (int i = 0; i < 10; i++)
                {
                    var w = exec.Worker;

                    var sw = Executors.NewShared(w);

                    try
                    {
                        for (int j = 0; j < 10; j++)
                        {
                            Flowable.Just(1).SubscribeOn(sw)
                                .Test()
                                .WithTag("SubscribeOn round " + i + "/" + j + " - " + w.GetType().Name)
                                .AwaitDone(TimeSpan.FromSeconds(5))
                                .AssertResult(1);

                            Flowable.Just(1).Delay(TimeSpan.FromMilliseconds(10))
                                .Test()
                                .WithTag("Delay round " + i + "/" + j + " - " + w.GetType().Name)
                                .AwaitDone(TimeSpan.FromSeconds(5))
                                .AssertResult(1);

                            Flowable.Interval(TimeSpan.FromMilliseconds(10))
                                .Take(5)
                                .Test()
                                .WithTag("Interval round " + i + "/" + j + " - " + w.GetType().Name)
                                .AwaitDone(TimeSpan.FromSeconds(5))
                                .AssertResult(0L, 1L, 2L, 3L, 4L);
                        }
                    }
                    finally
                    {
                        w.Dispose();
                    }
                }
            }
        }
    }
}
