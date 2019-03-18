using Reactive.Streams;
using Reactive4.NET.subscribers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reactive4.NET.operators
{
    sealed class FlowablePeek<T> : AbstractFlowableOperator<T, T>
    {
        readonly Action<T> onNext;

        readonly Action<T> onAfterNext;

        readonly Action<Exception> onError;

        readonly Action onComplete;

        readonly Action onTerminated;

        readonly Action onAfterTerminated;

        readonly Action<ISubscription> onSubscribe;

        readonly Action<long> onRequest;

        readonly Action onCancel;

        FlowablePeek(IFlowable<T> source,
                Action<T> onNext = null,
                Action<T> onAfterNext = null,
                Action<Exception> onError = null,
                Action onComplete = null,
                Action onTerminated = null,
                Action onAfterTerminated = null,
                Action<ISubscription> onSubscribe = null,
                Action<long> onRequest = null,
                Action onCancel = null) : base(source)
        {
            this.onNext = onNext;
            this.onAfterNext = onAfterNext;
            this.onError = onError;
            this.onComplete = onComplete;
            this.onTerminated = onTerminated;
            this.onAfterTerminated = onAfterTerminated;
            this.onSubscribe = onSubscribe;
            this.onRequest = onRequest;
            this.onCancel = onCancel;
        }

        public override void Subscribe(IFlowableSubscriber<T> subscriber)
        {
            if (subscriber is IConditionalSubscriber<T> s)
            {
                source.Subscribe(new PeekConditionalSubscriber(s, this));
            }
            else
            {
                source.Subscribe(new PeekSubscriber(subscriber, this));
            }
        }

        static Action<E> Combine<E>(Action<E> existing, Action<E> next)
        {
            if (existing != null)
            {
                if (next != null)
                {
                    var n = next;
                    return v => { existing(v); n(v); };
                }
                return existing;
            }
            return next;
        }

        static Action Combine(Action existing, Action next)
        {
            if (existing != null)
            {
                if (next != null)
                {
                    var n = next;
                    return ()=> { existing(); n(); };
                }
                return existing;
            }
            return next;
        }

        internal static FlowablePeek<T> Create(IFlowable<T> source,
                Action<T> onNext = null,
                Action<T> onAfterNext = null,
                Action<Exception> onError = null,
                Action onComplete = null,
                Action onTerminated = null,
                Action onAfterTerminated = null,
                Action<ISubscription> onSubscribe = null,
                Action<long> onRequest = null,
                Action onCancel = null)
        {
            if (source is FlowablePeek<T> s)
            {
                source = s.source;
                onNext = Combine(s.onNext, onNext);
                onAfterNext = Combine(s.onAfterNext, onAfterNext);
                onError = Combine(s.onError, onError);
                onComplete = Combine(s.onComplete, onComplete);
                onTerminated = Combine(s.onTerminated, onTerminated);
                onAfterTerminated = Combine(s.onAfterTerminated, onAfterTerminated);
                onSubscribe = Combine(s.onSubscribe, onSubscribe);
                onRequest = Combine(s.onRequest, onRequest);
                onCancel = Combine(s.onCancel, onCancel);
            }
            return new FlowablePeek<T>(source, onNext, onAfterNext, onError, onComplete, onTerminated, onAfterTerminated, onSubscribe, onRequest, onCancel);
        }

        sealed class PeekSubscriber : AbstractFuseableSubscriber<T, T>
        {
            readonly IFlowableSubscriber<T> actual;

            readonly FlowablePeek<T> parent;

            internal PeekSubscriber(IFlowableSubscriber<T> actual, FlowablePeek<T> parent)
            {
                this.actual = actual;
                this.parent = parent;
            }

            void AfterTerminated()
            {
                try
                {
                    parent.onAfterTerminated?.Invoke();
                }
                catch
                {
                    // TODO what to do with these?
                }
            }

            public override void OnComplete()
            {
                if (done)
                {
                    return;
                }
                done = true;
                try
                {
                    parent.onComplete?.Invoke();
                    parent.onTerminated?.Invoke();
                }
                catch (Exception ex)
                {
                    actual.OnError(ex);
                    AfterTerminated();
                    return;
                }

                actual.OnComplete();

                AfterTerminated();
            }

            public override void OnError(Exception cause)
            {
                if (done)
                {
                    return;
                }
                done = true;
                try
                {
                    parent.onError?.Invoke(cause);
                    parent.onTerminated?.Invoke();
                }
                catch (Exception ex)
                {
                    cause = new AggregateException(cause, ex);
                }

                actual.OnError(cause);

                AfterTerminated();
            }

            public override void OnNext(T element)
            {
                if (done)
                {
                    return;
                }
                if (fusionMode == FusionSupport.NONE)
                {
                    try
                    {
                        parent.onNext?.Invoke(element);
                    }
                    catch (Exception ex)
                    {
                        upstream.Cancel();
                        OnError(ex);
                        return;
                    }
                }

                actual.OnNext(element);

                if (fusionMode == FusionSupport.NONE)
                {
                    try
                    {
                        parent.onAfterNext?.Invoke(element);
                    }
                    catch (Exception ex)
                    {
                        upstream.Cancel();
                        OnError(ex);
                        return;
                    }
                }
            }

            public override bool Poll(out T item)
            {
                if (qs.Poll(out item))
                {
                    if (fusionMode == FusionSupport.SYNC)
                    {
                        parent.onNext?.Invoke(item);
                        parent.onAfterNext?.Invoke(item);
                    }
                    return true;
                }
                if (fusionMode == FusionSupport.SYNC)
                {
                    parent.onComplete?.Invoke();
                    parent.onTerminated?.Invoke();
                    parent.onAfterTerminated?.Invoke();
                }
                return false;
            }

            protected override void OnStart(ISubscription subscription)
            {
                try
                {
                    parent.onSubscribe?.Invoke(subscription);
                }
                catch (Exception ex)
                {
                    subscription.Cancel();
                    OnError(ex);
                    return;
                }
                actual.OnSubscribe(this);
            }

            public override void Cancel()
            {
                try
                {
                    parent.onCancel?.Invoke();
                }
                catch
                {
                    // TODO what to do with these?
                }
                base.Cancel();
            }

            public override void Request(long n)
            {
                try
                {
                    parent.onRequest?.Invoke(n);
                }
                catch
                {
                    // TODO what to do with these?
                }
                base.Request(n);
            }
        }

        sealed class PeekConditionalSubscriber : AbstractFuseableConditionalSubscriber<T, T>
        {
            readonly IConditionalSubscriber<T> actual;

            readonly FlowablePeek<T> parent;

            internal PeekConditionalSubscriber(IConditionalSubscriber<T> actual, FlowablePeek<T> parent)
            {
                this.actual = actual;
                this.parent = parent;
            }

            void AfterTerminated()
            {
                try
                {
                    parent.onAfterTerminated?.Invoke();
                }
                catch
                {
                    // TODO what to do with these?
                }
            }

            public override void OnComplete()
            {
                if (done)
                {
                    return;
                }
                done = true;
                try
                {
                    parent.onComplete?.Invoke();
                    parent.onTerminated?.Invoke();
                }
                catch (Exception ex)
                {
                    actual.OnError(ex);
                    AfterTerminated();
                    return;
                }

                actual.OnComplete();

                AfterTerminated();
            }

            public override void OnError(Exception cause)
            {
                if (done)
                {
                    return;
                }
                done = true;
                try
                {
                    parent.onError?.Invoke(cause);
                    parent.onTerminated?.Invoke();
                }
                catch (Exception ex)
                {
                    cause = new AggregateException(cause, ex);
                }

                actual.OnError(cause);

                AfterTerminated();
            }

            public override bool TryOnNext(T element)
            {
                if (done)
                {
                    return false;
                }
                if (fusionMode == FusionSupport.NONE)
                {
                    try
                    {
                        parent.onNext?.Invoke(element);
                    }
                    catch (Exception ex)
                    {
                        upstream.Cancel();
                        OnError(ex);
                        return false;
                    }
                }

                bool b = actual.TryOnNext(element);

                if (fusionMode == FusionSupport.NONE)
                {
                    try
                    {
                        parent.onAfterNext?.Invoke(element);
                    }
                    catch (Exception ex)
                    {
                        upstream.Cancel();
                        OnError(ex);
                        return false;
                    }
                }
                return b;
            }

            public override bool Poll(out T item)
            {
                if (qs.Poll(out item))
                {
                    if (fusionMode == FusionSupport.SYNC)
                    {
                        parent.onNext?.Invoke(item);
                        parent.onAfterNext?.Invoke(item);
                    }
                    return true;
                }
                if (fusionMode == FusionSupport.SYNC)
                {
                    parent.onComplete?.Invoke();
                    parent.onTerminated?.Invoke();
                    parent.onAfterTerminated?.Invoke();
                }
                return false;
            }

            protected override void OnStart(ISubscription subscription)
            {
                try
                {
                    parent.onSubscribe?.Invoke(subscription);
                }
                catch (Exception ex)
                {
                    subscription.Cancel();
                    OnError(ex);
                    return;
                }
                actual.OnSubscribe(this);
            }

            public override void Cancel()
            {
                try
                {
                    parent.onCancel?.Invoke();
                }
                catch
                {
                    // TODO what to do with these?
                }
                base.Cancel();
            }

            public override void Request(long n)
            {
                try
                {
                    parent.onRequest?.Invoke(n);
                }
                catch
                {
                    // TODO what to do with these?
                }
                base.Request(n);
            }
        }
    }
}
