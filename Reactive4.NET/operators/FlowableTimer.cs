using Reactive4.NET.utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reactive4.NET.operators
{
    sealed class FlowableTimer : AbstractFlowableSource<long>
    {
        readonly IExecutorService executor;

        readonly TimeSpan delay;

        internal FlowableTimer(TimeSpan delay, IExecutorService executor)
        {
            this.executor = executor;
            this.delay = delay;
        }

        public override void Subscribe(IFlowableSubscriber<long> subscriber)
        {
            var parent = new TimerSubscription(subscriber);
            subscriber.OnSubscribe(parent);

            parent.SetTask(executor.Schedule(parent.Run, delay));
        }

        sealed class TimerSubscription : AbstractDeferredScalarSubscription<long>
        {
            IDisposable task;

            public TimerSubscription(IFlowableSubscriber<long> actual) : base(actual)
            {
            }

            internal void SetTask(IDisposable d)
            {
                DisposableHelper.Replace(ref task, d);
            }

            public override void Cancel()
            {
                base.Cancel();
                DisposableHelper.Dispose(ref task);
            }

            internal void Run()
            {
                Complete(0L);
            }
        }
    }
}
