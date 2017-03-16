using Reactive.Streams;
using Reactive4.NET.operators;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Reactive4.NET.utils
{
    sealed class BooleanSubscription : ISubscription
    {
        int cancelled;

        long requested;

        public long Requested => Volatile.Read(ref requested);

        public bool IsCancelled => Volatile.Read(ref cancelled) != 0;

        public void Cancel()
        {
            Interlocked.Exchange(ref cancelled, 1);
        }

        public void Request(long n)
        {
            if (SubscriptionHelper.Validate(n))
            {
                SubscriptionHelper.AddRequest(ref requested, n);
            }
        }

        public void Produced(long n)
        {
            if (SubscriptionHelper.Validate(n))
            {
                SubscriptionHelper.Produced(ref requested, n);
            }
        }
    }
}
