﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Reactive.Streams;
using NUnit.Framework;

namespace Reactive4.NET.Test.Tck
{
    [TestFixture]
    class FlowableDelaySubscription2Tck : FlowableVerification<int>
    {
        public FlowableDelaySubscription2Tck() : base(100) { }

        public override IPublisher<int> CreatePublisher(long elements)
        {
            return Flowable.Range(1, (int)elements).DelaySubscription(TimeSpan.FromMilliseconds(1));
        }
    }
}
