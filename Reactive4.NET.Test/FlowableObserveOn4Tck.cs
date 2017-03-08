using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Reactive.Streams;
using NUnit.Framework;

namespace Reactive4.NET.Test
{
    [TestFixture]
    class FlowableObserveOn4Tck : FlowableVerification<int>
    {
        //public FlowableObserveOn4Tck() : base(250) { }

        public override IPublisher<int> CreatePublisher(long elements)
        {
            return Flowable.Range(1, (int)elements).Hide().ObserveOn(Executors.Task, 1);
        }
    }
}
