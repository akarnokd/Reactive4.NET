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
    class FlowableArrayTck : FlowableVerification<int>
    {
        public override IPublisher<int> CreatePublisher(long elements)
        {
            int[] a = new int[(int)elements];
            for (int i = 0; i < a.Length; i++)
            {
                a[i] = i;
            }
            return Flowable.FromArray(a);
        }
    }
}
