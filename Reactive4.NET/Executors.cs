using Reactive4.NET.schedulers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reactive4.NET
{
    public static class Executors
    {

        public static IExecutorService Single
        {
            get
            {
                return SingleExecutorService.Instance;
            }
        }

        public static IExecutorService Computation {
            get
            {
                return ComputationExecutorService.Instance;
            }
        }

        public static IExecutorService IO
        {
            get
            {
                return IOExecutorService.Instance;
            }
        }

        public static IExecutorService Thread
        {
            get
            {
                return ThreadExecutorService.Instance;
            }
        }

        public static IExecutorService Trampoline
        {
            get
            {
                return TrampolineExecutorService.Instance;
            }
        }

        public static IExecutorService Task
        {
            get {
                return TaskExecutorService.Instance;
            }
        }

        internal static IExecutorService Immediate
        {
            get
            {
                return ImmediateExecutorService.Instance;
            }
        }

        // ----------------------------------------------------------------------------

        public static IExecutorService NewParallel()
        {
            return null; // TODO return proper
        }

        public static IExecutorService NewParallel(int paralellism)
        {
            return null; // TODO return proper
        }

        public static TestExecutor NewTest()
        {
            return new TestExecutor();
        }

        public static IExecutorService NewShared(IExecutorWorker worker)
        {
            return null; // TODO return proper
        }

        public static IExecutorService NewBlocking()
        {
            return null; // TODO return proper
        }

        public static IExecutorService NewBlocking(Action initialTask)
        {
            return null; // TODO return proper
        }
    }
}
