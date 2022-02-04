﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Reactive.Streams;
using NUnit.Framework;
using System.Threading;
using Reactive4.NET.utils;

namespace Reactive4.NET.Test.Tck
{
    [TestFixture]
    class FlowableCreateErrorAll1Tck : FlowableVerification<int>
    {
        public FlowableCreateErrorAll1Tck() : base(100) { }

        public override IPublisher<int> CreatePublisher(long elements)
        {
            return Flowable.Create<int>(e =>
            {
                var cts = new CancellationTokenSource();
                e.OnCancel(cts.Dispose);

                Task.Factory.StartNew(() => {
                    long now = SchedulerHelper.NowUTC();

                    long f = 0;

                    for (int i = 0; i < elements; i++)
                    {
                        while (e.Requested == f)
                        {
                            if (e.IsCancelled)
                            {
                                return;
                            }
                            Thread.Sleep(1);
                            if (SchedulerHelper.NowUTC() - now > 1000)
                            {
                                return;
                            }
                        }

                        if (e.IsCancelled)
                        {
                            return;
                        }

                        e.OnNext(i);

                        f++;
                    }
                    if (!e.IsCancelled)
                    {
                        e.OnComplete();
                    }
                }, cts.Token);
            }, BackpressureStrategy.ERROR);
        }
    }
}
