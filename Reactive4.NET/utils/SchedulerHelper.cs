using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reactive4.NET.utils
{
    internal static class SchedulerHelper
    {
        static readonly DateTime Epoch = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);

        internal static long ToUTC(DateTime time)
        {
            return (long)(time - Epoch).TotalMilliseconds;
        }

        internal static long NowUTC()
        {
            return ToUTC(DateTime.UtcNow);
        }
    }
}
