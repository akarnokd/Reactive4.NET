using Reactive4.NET.operators;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reactive4.NET.subscribers
{
    sealed class BlockingLastSubscriber<T> : AbstractBlockingSubscriber<T>
    {
        public override void OnNext(T element)
        {
            hasItem = true;
            item = element;
        }
    }
}
