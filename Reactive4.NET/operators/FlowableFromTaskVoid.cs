using Reactive4.NET.utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Reactive4.NET.operators
{
    sealed class FlowableFromTaskVoid : AbstractFlowableSource<object>
    {
        readonly Task task;

        internal FlowableFromTaskVoid(Task task)
        {
            this.task = task;
        }

        public override void Subscribe(IFlowableSubscriber<object> subscriber)
        {
            var s = new FromTaskSubscription(subscriber);
            subscriber.OnSubscribe(s);

            task.ContinueWith(t => s.Run(t), s.cts.Token);
        }

        sealed class FromTaskSubscription : AbstractDeferredScalarSubscription<object>
        {
            internal readonly CancellationTokenSource cts;

            public FromTaskSubscription(IFlowableSubscriber<object> actual) : base(actual)
            {
                this.cts = new CancellationTokenSource();
            }

            internal void Run(Task task)
            {
                if (task.IsFaulted)
                {
                    Error(task.Exception);
                }
                else
                if (task.IsCompleted)
                {
                    Complete();
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
