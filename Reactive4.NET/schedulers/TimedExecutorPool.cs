using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reactive4.NET.schedulers
{
    static class TimedExecutorPool
    {
        static readonly int PoolCount;

        static readonly TimedBlockingExecutor[] pools;

        static int index;

        static TimedExecutorPool()
        {
            PoolCount = Math.Max(1, Math.Min(Environment.ProcessorCount / 2, 4));
            pools = new TimedBlockingExecutor[PoolCount];
            for (int i = 0; i < PoolCount; i++)
            {
                pools[i] = new TimedBlockingExecutor("TimedExecutorWorker-" + (i + 1));
            }
        }

        internal static TimedBlockingExecutor TimedExecutor {
            get
            {
                int i = 0;
                var p = pools[i];
                i++;
                index = i == PoolCount ? 0 : i;
                p.Start();
                return p;
            }
        }
    }
}
