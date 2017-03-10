using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
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

        internal static IDisposable ScheduleTask(Action task)
        {
            var cts = new CancellationTokenSource();
            Task.Run(task, cts.Token);
            return cts;
        }

        internal static IDisposable ScheduleTask(Action task, TimeSpan delay)
        {
            var cts = new CancellationTokenSource();
            if (delay == TimeSpan.Zero)
            {
                Task.Run(task, cts.Token);
            }
            else
            {
                Task.Delay(delay, cts.Token).ContinueWith(a => task(), cts.Token);
            }
            return cts;
        }

        internal static IDisposable ScheduleTask(Action task, TimeSpan initialDelay, TimeSpan period)
        {
            var cts = new CancellationTokenSource();

            Action<Task> recursive = null;
            long now = NowUTC() + (long)initialDelay.TotalMilliseconds;
            long[] round = { 0 };
            recursive = t =>
            {
                task();
                long next = (long)(now + (++round[0]) * period.TotalMilliseconds - NowUTC());
                Task.Delay(TimeSpan.FromMilliseconds(Math.Max(0L, next)), cts.Token).ContinueWith(recursive, cts.Token);
            };

            Task.Delay(initialDelay, cts.Token)
                .ContinueWith(recursive, cts.Token);

            return cts;
        }
    }
}
