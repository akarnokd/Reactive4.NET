using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Reactive.Streams;

namespace Reactive4.NET.operators
{
    sealed class ParallelFlowablePeek<T> : AbstractParallelOperator<T, T>
    {
        readonly Action<T> onNext;

        readonly Action<T> onAfterNext;

        readonly Action<Exception> onError;

        readonly Action onComplete;

        readonly Action onTerminated;

        readonly Action onAfterTerminated;

        public ParallelFlowablePeek(IParallelFlowable<T> source,
                Action<T> onNext,
                Action<T> onAfterNext,
                Action<Exception> onError,
                Action onComplete,
                Action onTerminated,
                Action onAfterTerminated
            ) : base(source)
        {
            this.onNext = onNext;
            this.onAfterNext = onAfterNext;
            this.onError = onError;
            this.onComplete = onComplete;
            this.onTerminated = onTerminated;
            this.onAfterTerminated = onAfterTerminated;
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
                    return () => { existing(); n(); };
                }
                return existing;
            }
            return next;
        }

        internal static ParallelFlowablePeek<T> Create(IParallelFlowable<T> source,
                Action<T> onNext = null,
                Action<T> onAfterNext = null,
                Action<Exception> onError = null,
                Action onComplete = null,
                Action onTerminated = null,
                Action onAfterTerminated = null)
        {
            if (source is ParallelFlowablePeek<T> s)
            {
                source = s.source;
                onNext = Combine(s.onNext, onNext);
                onAfterNext = Combine(s.onAfterNext, onAfterNext);
                onError = Combine(s.onError, onError);
                onComplete = Combine(s.onComplete, onComplete);
                onTerminated = Combine(s.onTerminated, onTerminated);
                onAfterTerminated = Combine(s.onAfterTerminated, onAfterTerminated);
            }
            return new ParallelFlowablePeek<T>(source, onNext, onAfterNext, onError, onComplete, onTerminated, onAfterTerminated);
        }

        public override void Subscribe(IFlowableSubscriber<T>[] subscribers)
        {
            if (Validate(subscribers))
            {
                int n = subscribers.Length;
                var parents = new IFlowableSubscriber<T>[n];

                for (int i = 0; i < n; i++)
                {
                    var s = subscribers[i];
                    if (s is IConditionalSubscriber<T> cs)
                    {
                        parents[i] = new PeekConditionalSubscriber(cs, this);
                    }
                    else
                    {
                        parents[i] = new PeekSubscriber(s, this);
                    }
                }

                source.Subscribe(parents);
            }
        }

        sealed class PeekSubscriber : IFlowableSubscriber<T>, ISubscription
        {
            readonly IFlowableSubscriber<T> actual;

            readonly Action<T> onNext;

            readonly Action<T> onAfterNext;

            readonly ParallelFlowablePeek<T> parent;

            ISubscription upstream;

            bool done;

            internal PeekSubscriber(IFlowableSubscriber<T> actual, ParallelFlowablePeek<T> parent)
            {
                this.actual = actual;
                this.parent = parent;
                this.onNext = parent.onNext;
                this.onAfterNext = parent.onAfterNext;
            }

            public void Cancel()
            {
                upstream.Cancel();
            }

            public void OnComplete()
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
                    OnAfterTerminate();
                    return;
                }

                actual.OnComplete();

                OnAfterTerminate();
            }

            void OnAfterTerminate()
            {
                try
                {
                    parent.onAfterTerminated?.Invoke();
                }
                catch
                {
                    // TODO what should happen with these?
                }
            }

            public void OnError(Exception cause)
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

                OnAfterTerminate();
            }

            public void OnNext(T element)
            {
                if (done)
                {
                    return;
                }

                try
                {
                    onNext?.Invoke(element);
                }
                catch (Exception ex)
                {
                    upstream.Cancel();
                    OnError(ex);
                    return;
                }

                actual.OnNext(element);

                try
                {
                    onAfterNext?.Invoke(element);
                }
                catch (Exception ex)
                {
                    upstream.Cancel();
                    OnError(ex);
                    return;
                }

            }

            public void OnSubscribe(ISubscription subscription)
            {
                if (SubscriptionHelper.Validate(ref upstream, subscription))
                {
                    actual.OnSubscribe(this);
                }
            }

            public void Request(long n)
            {
                upstream.Request(n);
            }
        }

        sealed class PeekConditionalSubscriber : IConditionalSubscriber<T>, ISubscription
        {
            readonly IConditionalSubscriber<T> actual;

            readonly Action<T> onNext;

            readonly Action<T> onAfterNext;

            readonly ParallelFlowablePeek<T> parent;

            ISubscription upstream;

            bool done;

            internal PeekConditionalSubscriber(IConditionalSubscriber<T> actual, ParallelFlowablePeek<T> parent)
            {
                this.actual = actual;
                this.parent = parent;
                this.onNext = parent.onNext;
                this.onAfterNext = parent.onAfterNext;
            }

            public void Cancel()
            {
                upstream.Cancel();
            }

            public void OnComplete()
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
                    OnAfterTerminate();
                    return;
                }

                actual.OnComplete();

                OnAfterTerminate();
            }

            void OnAfterTerminate()
            {
                try
                {
                    parent.onAfterTerminated?.Invoke();
                }
                catch
                {
                    // TODO what should happen with these?
                }
            }

            public void OnError(Exception cause)
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

                OnAfterTerminate();
            }

            public void OnNext(T element)
            {
                if (!TryOnNext(element) && !done)
                {
                    upstream.Request(1);
                }
            }

            public void OnSubscribe(ISubscription subscription)
            {
                if (SubscriptionHelper.Validate(ref upstream, subscription))
                {
                    actual.OnSubscribe(this);
                }
            }

            public void Request(long n)
            {
                upstream.Request(n);
            }

            public bool TryOnNext(T element)
            {
                if (done)
                {
                    return false;
                }

                try
                {
                    onNext?.Invoke(element);
                }
                catch (Exception ex)
                {
                    upstream.Cancel();
                    OnError(ex);
                    return false;
                }

                bool b = actual.TryOnNext(element);

                try
                {
                    onAfterNext?.Invoke(element);
                }
                catch (Exception ex)
                {
                    upstream.Cancel();
                    OnError(ex);
                    return false;
                }

                return b;
            }
        }
    }
}
