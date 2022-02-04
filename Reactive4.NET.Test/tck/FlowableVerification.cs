using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Reactive.Streams;
using Reactive.Streams.TCK;
using Reactive.Streams.TCK.Support;
using Reactive4.NET;
using System.Collections;

namespace Reactive4.NET.Test.Tck
{
    [TestFixture]
    abstract class FlowableVerification<T> : PublisherVerification<T>
    {
        protected FlowableVerification() : base(new TestEnvironment(25))
        {
        }

        protected FlowableVerification(int timeout) : base(new TestEnvironment(timeout))
        {
        }

        public override long MaxElementsFromPublisher => 1024;

        public override IPublisher<T> CreateFailedPublisher() => Flowable.Error<T>(new TestException());

        protected IEnumerable<int> Enumerate(long elements) => Enumerate(elements > int.MaxValue, elements);

        protected IEnumerable<int> Enumerate(bool useInfinite, long elements)
            => useInfinite
                ? new InfiniteEnumerable()
                : Enumerable.Range(0, (int)elements);


        private sealed class InfiniteEnumerable : IEnumerable<int>
        {
            public IEnumerator<int> GetEnumerator() => new InfiniteEnumerator();

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

            private sealed class InfiniteEnumerator : IEnumerator<int>
            {
                private int _current;

                public void Dispose()
                {

                }

                public bool MoveNext()
                {
                    _current++;
                    return true;
                }

                public void Reset() => _current = 0;

                public int Current => _current;

                object IEnumerator.Current => Current;
            }
        }
    }
}
