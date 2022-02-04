using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Reactive.Streams;
using NUnit.Framework;

namespace Reactive4.NET.Test.Tck
{
    [TestFixture]
    class FlowableSwitchIfEmptyEnumerable2Tck : FlowableVerification<int>
    {
        public override IPublisher<int> CreatePublisher(long elements)
        {
            return Flowable.Empty<int>().SwitchIfEmpty(Sequence(elements));
        }

        IEnumerable<IFlowable<int>> Sequence(long elements)
        {
            yield return Flowable.Empty<int>();
            yield return Flowable.Empty<int>();
            yield return Flowable.Range(1, (int)elements);
        }
    }
}
