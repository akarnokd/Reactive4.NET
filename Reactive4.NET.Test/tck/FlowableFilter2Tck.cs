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
    class FlowableFilter2Tck : FlowableVerification<int>
    {
        public override IPublisher<int> CreatePublisher(long elements)
        {
            return Flowable.Range(0, (int)elements * 2).Filter(v => v % 2 == 0).Filter(v => true);
        }
    }
}
