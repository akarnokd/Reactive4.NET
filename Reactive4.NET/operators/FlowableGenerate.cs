using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Reactive4.NET.operators
{
    sealed class FlowableGenerate<T, S> : AbstractFlowableSource<T>
    {
        readonly Func<S> stateFactory;

        readonly Func<S, IGeneratorEmitter<T>, S> emitter;

        readonly Action<S> stateCleanup;

        readonly bool eager;

        internal FlowableGenerate(Func<S> stateFactory, Func<S, IGeneratorEmitter<T>, S> emitter, Action<S> stateCleanup, bool eager)
        {
            this.stateFactory = stateFactory;
            this.emitter = emitter;
            this.stateCleanup = stateCleanup;
            this.eager = eager;
        }

        public override void Subscribe(IFlowableSubscriber<T> subscriber)
        {
            S state;

            try
            {
                state = stateFactory();
            }
            catch (Exception ex)
            {
                subscriber.OnSubscribe(EmptySubscription<T>.Instance);
                subscriber.OnError(ex);
                return;
            }

            subscriber.OnSubscribe(new GenerateSubscriber(subscriber, state, emitter, stateCleanup, eager));
        }

        sealed class GenerateSubscriber: IQueueSubscription<T>, IGeneratorEmitter<T>
        {
            readonly IFlowableSubscriber<T> actual;

            readonly Func<S, IGeneratorEmitter<T>, S> emitter;

            readonly Action<S> stateCleanup;

            readonly bool eager;

            long requested;

            bool cancelled;

            S state;

            T value;
            bool hasValue;

            bool done;
            Exception error;

            int once;

            internal GenerateSubscriber(IFlowableSubscriber<T> actual, S state, Func<S, IGeneratorEmitter<T>, S> emitter, Action<S> stateCleanup, bool eager)
            {
                this.actual = actual;
                this.state = state;
                this.emitter = emitter;
                this.stateCleanup = stateCleanup;
                this.eager = eager;
            }

            public void Cancel()
            {
                Volatile.Write(ref cancelled, true);
                if (Interlocked.Increment(ref requested) == 1)
                {
                    if (Interlocked.CompareExchange(ref once, 1, 0) == 0)
                    {
                        stateCleanup?.Invoke(state);
                    }
                }
            }

            public void Clear()
            {
                value = default(T);
                hasValue = false;
                done = true;
            }

            public bool IsEmpty()
            {
                return done && !hasValue && error == null;
            }

            public bool Offer(T item)
            {
                throw new InvalidOperationException("Should not be called!");
            }

            public void OnComplete()
            {
                if (!done)
                {
                    done = true;
                }
            }

            public void OnError(Exception error)
            {
                if (!done)
                {
                    this.error = error;
                    done = true;
                }
            }

            public void OnNext(T item)
            {
                if (!done)
                {
                    if (!hasValue)
                    {
                        value = item;
                        hasValue = true;
                    }
                    else
                    {
                        var err = new InvalidOperationException("Only one OnNext() is allowed at a time");
                        var ex = error;
                        if (ex == null)
                        {
                            error = err;
                        } else
                        {
                            error = new AggregateException(ex, err);
                        }

                        done = true;
                    }
                }
            }

            public bool Poll(out T item)
            {
                Exception ex;
                if (done)
                {
                    return FusedCleanup(out item);
                }
                state = emitter(state, this);

                if (hasValue)
                {
                    item = value;
                    value = default(T);
                    hasValue = false;
                    return true;
                }
                else
                if (!done)
                {
                    var m = new InvalidOperationException("OnNext() not called");
                    ex = error;
                    if (ex == null)
                    {
                        ex = m;
                    }
                    else
                    {
                        ex = new AggregateException(ex, m);
                    }
                    done = true;
                }
                return FusedCleanup(out item);
            }

            bool FusedCleanup(out T item)
            {
                Exception ex;
                if (eager)
                {
                    try
                    {
                        if (Interlocked.CompareExchange(ref once, 1, 0) == 0)
                        {
                            stateCleanup?.Invoke(state);
                        }
                    }
                    catch (Exception ex2)
                    {
                        ex = error;
                        if (ex == null)
                        {
                            ex = ex2;
                        }
                        else
                        {
                            ex = new AggregateException(ex, ex2);
                        }
                    }
                }
                try
                {
                    ex = error;
                    if (ex != null)
                    {
                        error = null;

                        throw ex;
                    }
                    item = default(T);
                    return false;
                }
                finally
                {
                    if (!eager)
                    {
                        CleanupAfter(state);
                    }
                }
            }

            public void Request(long n)
            {
                if (SubscriptionHelper.Validate(n))
                {
                    if (SubscriptionHelper.AddRequest(ref requested, n) == 0)
                    {
                        Drain(n);
                    }
                }
            }

            public int RequestFusion(int mode)
            {
                return mode & FusionSupport.SYNC;
            }

            void CleanupAfter(S s)
            {
                try
                {
                    if (Interlocked.CompareExchange(ref once, 1, 0) == 0)
                    {
                        stateCleanup?.Invoke(state);
                    }
                }
                catch
                {
                    // TODO what to do with these
                }
            }

            void Drain(long n)
            {
                var s = state;
                var g = emitter;
                var a = actual;
                for (;;)
                {
                    long e = 0L;
                    while (e != n)
                    {
                        if (Volatile.Read(ref cancelled))
                        {
                            CleanupAfter(s);
                            return;
                        }

                        try
                        {
                            s = emitter(s, this);
                        }
                        catch (Exception exc)
                        {
                            if (error == null)
                            {
                                error = exc;
                            }
                            done = true;
                        }

                        if (hasValue)
                        {
                            hasValue = false;
                            var v = value;
                            value = default(T);
                            a.OnNext(v);
                            e++;
                        }
                        else
                        if (!done)
                        {
                            var m = new InvalidOperationException("OnNext() not called");
                            var ex = error;
                            if (ex == null)
                            {
                                ex = m;
                            }
                            else
                            {
                                ex = new AggregateException(ex, m);
                            }
                            done = true;
                        }

                        if (done)
                        {
                            var ex = error;
                            if (ex != null)
                            {
                                if (eager)
                                {
                                    try
                                    {
                                        if (Interlocked.CompareExchange(ref once, 1, 0) == 0)
                                        {
                                            stateCleanup?.Invoke(state);
                                        }
                                    }
                                    catch (Exception ex2)
                                    {
                                        ex = new AggregateException(ex, ex2);
                                    }
                                }
                                a.OnError(ex);
                            }
                            else
                            {
                                if (eager)
                                {
                                    try
                                    {
                                        if (Interlocked.CompareExchange(ref once, 1, 0) == 0)
                                        {
                                            stateCleanup?.Invoke(state);
                                        }
                                    }
                                    catch (Exception ex2)
                                    {
                                        a.OnError(ex2);
                                        return;
                                    }
                                }
                                a.OnComplete();
                            }
                            if (!eager)
                            {
                                CleanupAfter(s);
                            }
                            return;
                        }
                    }

                    n = Volatile.Read(ref requested);
                    if (e == n)
                    {
                        state = s;
                        n = Interlocked.Add(ref requested, -n);
                        if (n == 0L)
                        {
                            break;
                        }
                    }
                }
            }
        }
    }
}
