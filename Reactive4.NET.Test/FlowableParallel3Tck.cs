﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Reactive.Streams;
using NUnit.Framework;

namespace Reactive4.NET.Test
{
    [TestFixture]
    class FlowableParallel3Tck : FlowableVerification<int>
    {
        public FlowableParallel3Tck() : base(200) { }

        public override IPublisher<int> CreatePublisher(long elements)
        {
            return Flowable.Range(1, (int)elements)
                .Parallel()
                .RunOn(Executors.Computation)
                .Sequential();
        }
    }
}
