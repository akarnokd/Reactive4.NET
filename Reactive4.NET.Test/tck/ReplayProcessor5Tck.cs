﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Reactive.Streams;
using NUnit.Framework;
using System.Threading;

namespace Reactive4.NET.Test.Tck
{
    [TestFixture]
    class ReplayProcessor5Tck : FlowableProcessorVerification<object>
    {
        public override object CreateElement(int element)
        {
            return element;
        }

        public override IProcessor<object, object> CreateIdentityProcessor(int bufferSize)
        {
            return new ReplayProcessor<object>().RefCount();
        }

        public override long MaxSupportedSubscribers => 1;
    }
}
