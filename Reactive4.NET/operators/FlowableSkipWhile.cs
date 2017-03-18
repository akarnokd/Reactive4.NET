using Reactive4.NET.subscribers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Reactive.Streams;

namespace Reactive4.NET.operators
{
    sealed class FlowableSkipWhile<T> : AbstractFlowableOperator<T, T>
    {
        readonly Func<T, bool> predicate;

        public FlowableSkipWhile(IFlowable<T> source, Func<T, bool> predicate) : base(source)
        {
            this.predicate = predicate;
        }

        public override void Subscribe(IFlowableSubscriber<T> subscriber)
        {
            if (subscriber is IConditionalSubscriber<T> s)
            {
                source.Subscribe(new SkipWhileConditionalSubscriber(s, predicate));
            }
            else
            {
                source.Subscribe(new SkipWhileSubscriber(subscriber, predicate));
            }
        }

        sealed class SkipWhileSubscriber : AbstractFuseableConditionalSubscriber<T, T>
        {
            readonly IFlowableSubscriber<T> actual;

            readonly Func<T, bool> predicate;

            bool gate;

            internal SkipWhileSubscriber(IFlowableSubscriber<T> actual, Func<T, bool> predicate)
            {
                this.actual = actual;
                this.predicate = predicate;
            }

            public override void OnComplete()
            {
                if (!done)
                {
                    done = true;
                    actual.OnComplete();
                }
            }

            public override void OnError(Exception cause)
            {
                if (!done)
                {
                    done = true;
                    actual.OnError(cause);
                }
            }

            public override bool Poll(out T item)
            {
                if (gate)
                {
                    return qs.Poll(out item);
                }
                for (;;)
                {
                    if (qs.Poll(out T v))
                    {
                        if (!predicate(v))
                        {
                            gate = true;
                            item = v;
                            return true;
                        }
                    }
                    else
                    {
                        break;
                    }
                }
                item = default(T);
                return false;
            }

            public override bool TryOnNext(T element)
            {
                if (done)
                {
                    return false;
                }
                if (fusionMode == FusionSupport.NONE)
                {
                    if (!gate)
                    {
                        bool b;

                        try
                        {
                            b = predicate(element);
                        }
                        catch (Exception ex)
                        {
                            upstream.Cancel();
                            OnError(ex);
                            return false;
                        }

                        if (!b)
                        {
                            gate = true;
                            actual.OnNext(element);
                            return true;
                        }
                        return false;
                    }
                    actual.OnNext(element);
                    return true;
                }

                actual.OnNext(default(T));
                return true;
            }

            protected override void OnStart(ISubscription subscription)
            {
                actual.OnSubscribe(this);
            }
        }
        sealed class SkipWhileConditionalSubscriber : AbstractFuseableConditionalSubscriber<T, T>
        {
            readonly IConditionalSubscriber<T> actual;

            readonly Func<T, bool> predicate;

            bool gate;

            internal SkipWhileConditionalSubscriber(IConditionalSubscriber<T> actual, Func<T, bool> predicate)
            {
                this.actual = actual;
                this.predicate = predicate;
            }

            public override void OnComplete()
            {
                if (!done)
                {
                    done = true;
                    actual.OnComplete();
                }
            }

            public override void OnError(Exception cause)
            {
                if (!done)
                {
                    done = true;
                    actual.OnError(cause);
                }
            }

            public override bool Poll(out T item)
            {
                if (gate)
                {
                    return qs.Poll(out item);
                }
                for (;;)
                {
                    if (qs.Poll(out T v))
                    {
                        if (!predicate(v))
                        {
                            gate = true;
                            item = v;
                            return true;
                        }
                    }
                    else
                    {
                        break;
                    }
                }
                item = default(T);
                return false;
            }

            public override bool TryOnNext(T element)
            {
                if (done)
                {
                    return false;
                }
                if (fusionMode == FusionSupport.NONE)
                {
                    if (!gate)
                    {
                        bool b;

                        try
                        {
                            b = predicate(element);
                        }
                        catch (Exception ex)
                        {
                            upstream.Cancel();
                            OnError(ex);
                            return false;
                        }

                        if (!b)
                        {
                            gate = true;
                            return actual.TryOnNext(element);
                        }
                        return false;
                    }
                    return actual.TryOnNext(element);
                }

                return actual.TryOnNext(default(T));
            }

            protected override void OnStart(ISubscription subscription)
            {
                actual.OnSubscribe(this);
            }
        }
    }
}
