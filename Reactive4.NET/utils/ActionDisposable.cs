using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Reactive4.NET.utils
{
    sealed class ActionDisposable : IDisposable
    {
        Action action;

        internal ActionDisposable(Action action)
        {
            this.action = action;
        }

        public void Dispose()
        {
            Interlocked.Exchange(ref action, null)?.Invoke();
        }
    }
}
