using Reactive4.NET.utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Reactive4.NET.operators
{
    sealed class FlowableFromTask<T> : AbstractFlowableSource<T>
    {
        readonly Task<T> task;

        internal FlowableFromTask(Task<T> task)
        {
            this.task = task;
        }

        public override void Subscribe(IFlowableSubscriber<T> subscriber)
        {
            var s = new FromTaskSubscription(subscriber);
            subscriber.OnSubscribe(s);

            task.ContinueWith(t => s.Run(t), s.cts.Token);
        }

        sealed class FromTaskSubscription : AbstractDeferredScalarSubscription<T>
        {
            internal readonly CancellationTokenSource cts;

            public FromTaskSubscription(IFlowableSubscriber<T> actual) : base(actual)
            {
                this.cts = new CancellationTokenSource();
            }

            internal void Run(Task<T> task)
            {
                if (task.IsFaulted)
                {
                    Error(task.Exception);
                }
                else
                if (task.IsCompleted)
                {
                    Complete(task.Result);
                }
            }

            public override void Cancel()
            {
                base.Cancel();
                cts.Dispose();
            }
        }
    }
}
