using Reactive4.NET.utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Reactive4.NET.schedulers
{
    static class ComputationExecutorService
    {
        internal static readonly IExecutorService Instance = new ParallelExecutorService();
    }
}
