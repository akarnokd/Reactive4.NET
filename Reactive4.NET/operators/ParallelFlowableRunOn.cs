using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Reactive.Streams;
using System.Threading;
using Reactive4.NET.utils;

namespace Reactive4.NET.operators
{
    sealed class ParallelFlowableRunOn<T> : AbstractParallelOperator<T, T>
    {
        readonly int bufferSize;

        readonly IExecutorService executor;

        public ParallelFlowableRunOn(IParallelFlowable<T> source, IExecutorService executor, int bufferSize) : base(source)
        {
            this.executor = executor;
            this.bufferSize = bufferSize;
        }

        public override void Subscribe(IFlowableSubscriber<T>[] subscribers)
        {
            if (Validate(subscribers))
            {
                var exec = executor;
                int n = subscribers.Length;
                IFlowableSubscriber<T>[] parents = new IFlowableSubscriber<T>[n];
                for (int i = 0; i < n; i++)
                {
                    var s = subscribers[i];

                    if (s is IConditionalSubscriber<T> cs)
                    {
                        parents[i] = new RunOnConditionalSubscriber(cs, bufferSize, exec.Worker);
                    }
                    else
                    {
                        parents[i] = new RunOnSubscriber(s, bufferSize, exec.Worker);
                    }
                }

                source.Subscribe(parents);
            }
        }

        internal abstract class AbstractRunOnSubscriber : IFlowableSubscriber<T>, ISubscription
        {
            readonly int bufferSize;

            internal readonly int limit;

            internal readonly IExecutorWorker worker;

            internal readonly ISimpleQueue<T> queue;

            readonly Action run;

            internal ISubscription upstream;

            internal long requested;

            internal long emitted;

            internal int wip;
            internal bool cancelled;
            internal bool done;
            internal Exception error;

            internal AbstractRunOnSubscriber(int bufferSize, IExecutorWorker worker)
            {
                this.bufferSize = bufferSize;
                this.worker = worker;
                this.limit = bufferSize - (bufferSize >> 2);
                this.queue = new SpscArrayQueue<T>(bufferSize);
                this.run = Drain;
            }

            public void Cancel()
            {
                Volatile.Write(ref cancelled, true);
                upstream.Cancel();
                worker.Dispose();
                if (Interlocked.Increment(ref wip) == 1)
                {
                    queue.Clear();
                }
            }

            public void OnComplete()
            {
                Volatile.Write(ref done, true);
                Schedule();
            }

            public void OnError(Exception cause)
            {
                error = cause;
                Volatile.Write(ref done, true);
                Schedule();
            }

            public void OnNext(T element)
            {
                queue.Offer(element);
                Schedule();
            }

            public void OnSubscribe(ISubscription subscription)
            {
                if (SubscriptionHelper.Validate(ref upstream, subscription))
                {
                    OnSubscribe();

                    subscription.Request(bufferSize);
                }
            }

            public void Request(long n)
            {
                if (SubscriptionHelper.Validate(n))
                {
                    SubscriptionHelper.AddRequest(ref requested, n);
                    Schedule();
                }
            }

            void Schedule()
            {
                if (Interlocked.Increment(ref wip) == 1)
                {
                    worker.Schedule(run);
                }
            }

            internal abstract void OnSubscribe();

            internal abstract void Drain();
        }

        sealed class RunOnSubscriber : AbstractRunOnSubscriber
        {
            readonly IFlowableSubscriber<T> actual;

            int consumed;

            internal RunOnSubscriber(IFlowableSubscriber<T> actual, int bufferSize, IExecutorWorker worker) : base(bufferSize, worker)
            {
                this.actual = actual;
            }

            internal override void OnSubscribe()
            {
                actual.OnSubscribe(this);
            }

            internal override void Drain()
            {
                int missed = 1;
                var a = actual;
                var q = queue;
                var lim = limit;
                var c = consumed;
                var e = emitted;

                for (;;)
                {
                    long r = Volatile.Read(ref requested);

                    while (e != r)
                    {
                        if (Volatile.Read(ref cancelled))
                        {
                            q.Clear();
                            return;
                        }

                        bool d = Volatile.Read(ref done);
                        bool empty = !q.Poll(out T t);

                        if (d && empty)
                        {
                            var ex = error;
                            if (ex == null)
                            {
                                a.OnComplete();
                            }
                            else
                            {
                                a.OnError(ex);
                            }
                            worker.Dispose();
                            return;
                        }

                        if (empty)
                        {
                            break;
                        }

                        a.OnNext(t);

                        e++;

                        if (++c == lim)
                        {
                            c = 0;
                            upstream.Request(lim);
                        }
                    }

                    if (e == r)
                    {
                        if (Volatile.Read(ref cancelled))
                        {
                            q.Clear();
                            return;
                        }

                        if (Volatile.Read(ref done) && q.IsEmpty())
                        {
                            var ex = error;
                            if (ex == null)
                            {
                                a.OnComplete();
                            }
                            else
                            {
                                a.OnError(ex);
                            }
                            worker.Dispose();
                            return;
                        }
                    }

                    int w = Volatile.Read(ref wip);
                    if (w == missed)
                    {
                        consumed = c;
                        emitted = e;
                        missed = Interlocked.Add(ref wip, -missed);
                        if (missed == 0)
                        {
                            break;
                        }
                    }
                    else
                    {
                        missed = w;
                    }
                }
            }
        }

        sealed class RunOnConditionalSubscriber : AbstractRunOnSubscriber
        {
            readonly IConditionalSubscriber<T> actual;

            int consumed;

            internal RunOnConditionalSubscriber(IConditionalSubscriber<T> actual, int bufferSize, IExecutorWorker worker) : base(bufferSize, worker)
            {
                this.actual = actual;
            }

            internal override void OnSubscribe()
            {
                actual.OnSubscribe(this);
            }

            internal override void Drain()
            {
                int missed = 1;
                var a = actual;
                var q = queue;
                var lim = limit;
                var c = consumed;
                var e = emitted;

                for (;;)
                {
                    long r = Volatile.Read(ref requested);

                    while (e != r)
                    {
                        if (Volatile.Read(ref cancelled))
                        {
                            q.Clear();
                            return;
                        }

                        bool d = Volatile.Read(ref done);
                        bool empty = !q.Poll(out T t);

                        if (d && empty)
                        {
                            var ex = error;
                            if (ex == null)
                            {
                                a.OnComplete();
                            }
                            else
                            {
                                a.OnError(ex);
                            }
                            worker.Dispose();
                            return;
                        }

                        if (empty)
                        {
                            break;
                        }

                        if (a.TryOnNext(t))
                        {
                            e++;
                        }

                        if (++c == lim)
                        {
                            c = 0;
                            upstream.Request(lim);
                        }
                    }

                    if (e == r)
                    {
                        if (Volatile.Read(ref cancelled))
                        {
                            q.Clear();
                            return;
                        }

                        if (Volatile.Read(ref done) && q.IsEmpty())
                        {
                            var ex = error;
                            if (ex == null)
                            {
                                a.OnComplete();
                            }
                            else
                            {
                                a.OnError(ex);
                            }
                            worker.Dispose();
                            return;
                        }
                    }

                    int w = Volatile.Read(ref wip);
                    if (w == missed)
                    {
                        consumed = c;
                        emitted = e;
                        missed = Interlocked.Add(ref wip, -missed);
                        if (missed == 0)
                        {
                            break;
                        }
                    }
                    else
                    {
                        missed = w;
                    }
                }
            }
        }
    }
}
