using Reactive.Streams;
using Reactive4.NET.subscribers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Reactive4.NET.operators
{
    sealed class FlowableUsing<T, D> : AbstractFlowableSource<T>
    {
        readonly Func<D> resourceFactory;

        readonly Func<D, IPublisher<T>> resourceMapper;

        readonly Action<D> resourceCleanup;

        readonly bool eager;

        internal FlowableUsing(Func<D> resourceFactory, Func<D, IPublisher<T>> resourceMapper,
            Action<D> resourceCleanup, bool eager)
        {
            this.resourceFactory = resourceFactory;
            this.resourceMapper = resourceMapper;
            this.resourceCleanup = resourceCleanup;
            this.eager = eager;
        }

        public override void Subscribe(IFlowableSubscriber<T> subscriber)
        {
            D resource;
            IPublisher<T> p;

            try
            {
                resource = resourceFactory();
            }
            catch (Exception ex)
            {
                subscriber.OnSubscribe(EmptySubscription<T>.Instance);
                subscriber.OnError(ex);
                return;
            }

            try
            {
                p = resourceMapper(resource);
                if (p == null)
                {
                    throw new NullReferenceException("The resourceMapper returned a null IPublisher");
                }
            }
            catch (Exception ex)
            {
                if (eager)
                {
                    try
                    {
                        resourceCleanup?.Invoke(resource);
                    }
                    catch (Exception exc)
                    {
                        ex = new AggregateException(ex, exc);
                    }
                }
                subscriber.OnSubscribe(EmptySubscription<T>.Instance);
                subscriber.OnError(ex);
                if (!eager)
                {
                    try
                    {
                        resourceCleanup?.Invoke(resource);
                    }
                    catch
                    {
                        // TODO what to do with these?
                    }
                }
                return;
            }

            if (subscriber is IConditionalSubscriber<T> s)
            {
                p.Subscribe(new UsingConditionalSubscriber(s, resource, resourceCleanup, eager));
            }
            else
            {
                p.Subscribe(new UsingSubscriber(subscriber, resource, resourceCleanup, eager));
            }
        }

        sealed class UsingSubscriber : AbstractFuseableSubscriber<T, T>
        {
            readonly IFlowableSubscriber<T> actual;

            readonly D resource;

            readonly Action<D> resourceCleanup;

            readonly bool eager;

            int once;

            internal UsingSubscriber(IFlowableSubscriber<T> actual,
                D resource, Action<D> resourceCleanup, bool eager)
            {
                this.actual = actual;
                this.resource = resource;
                this.resourceCleanup = resourceCleanup;
                this.eager = eager;
            }

            public override void OnComplete()
            {
                if (eager)
                {
                    if (Interlocked.CompareExchange(ref once, 1, 0) == 0)
                    {
                        try
                        {
                            resourceCleanup?.Invoke(resource);
                        }
                        catch (Exception ex)
                        {
                            actual.OnError(ex);
                            return;
                        }
                    }
                }
                actual.OnComplete();
                if (!eager)
                {
                    DisposeAfter();
                }
            }

            public override void OnError(Exception cause)
            {
                if (eager)
                {
                    if (Interlocked.CompareExchange(ref once, 1, 0) == 0)
                    {
                        try
                        {
                            resourceCleanup?.Invoke(resource);
                        }
                        catch (Exception ex)
                        {
                            cause = new AggregateException(cause, ex);
                        }
                    }
                }
                actual.OnError(cause);
                if (!eager)
                {
                    DisposeAfter();
                }
            }

            public override void OnNext(T element)
            {
                actual.OnNext(element);
            }

            public override bool Poll(out T item)
            {
                if (qs.Poll(out item))
                {
                    return true;
                }

                if (fusionMode == FusionSupport.SYNC)
                {
                    if (Interlocked.CompareExchange(ref once, 1, 0) == 0)
                    {
                        // no need for try-catch as Poll is expected to throw
                        resourceCleanup?.Invoke(resource);
                    }
                }
                return false;
            }

            protected override void OnStart(ISubscription subscription)
            {
                actual.OnSubscribe(this);
            }

            public override void Cancel()
            {
                if (eager)
                {
                    if (Interlocked.CompareExchange(ref once, 1, 0) == 0)
                    {
                        try
                        {
                            resourceCleanup?.Invoke(resource);
                        }
                        catch
                        {
                            // what to do?
                        }
                    }
                }
                base.Cancel();
                if (!eager)
                {
                    DisposeAfter();
                }
            }

            void DisposeAfter()
            {
                if (Interlocked.CompareExchange(ref once, 1, 0) == 0)
                {
                    try
                    {
                        resourceCleanup?.Invoke(resource);
                    }
                    catch
                    {
                        // TODO what should happen with this?
                    }
                }
            }
        }

        sealed class UsingConditionalSubscriber : AbstractFuseableConditionalSubscriber<T, T>
        {
            readonly IConditionalSubscriber<T> actual;

            readonly D resource;

            readonly Action<D> resourceCleanup;

            readonly bool eager;

            int once;

            internal UsingConditionalSubscriber(IConditionalSubscriber<T> actual,
                D resource, Action<D> resourceCleanup, bool eager)
            {
                this.actual = actual;
                this.resource = resource;
                this.resourceCleanup = resourceCleanup;
                this.eager = eager;
            }

            public override void OnComplete()
            {
                if (eager)
                {
                    if (Interlocked.CompareExchange(ref once, 1, 0) == 0)
                    {
                        try
                        {
                            resourceCleanup?.Invoke(resource);
                        }
                        catch (Exception ex)
                        {
                            actual.OnError(ex);
                            return;
                        }
                    }
                }
                actual.OnComplete();
                if (!eager)
                {
                    DisposeAfter();
                }
            }

            public override void OnError(Exception cause)
            {
                if (eager)
                {
                    if (Interlocked.CompareExchange(ref once, 1, 0) == 0)
                    {
                        try
                        {
                            resourceCleanup?.Invoke(resource);
                        }
                        catch (Exception ex)
                        {
                            cause = new AggregateException(cause, ex);
                        }
                    }
                }
                actual.OnError(cause);
                if (!eager)
                {
                    DisposeAfter();
                }
            }

            public override void OnNext(T element)
            {
                actual.OnNext(element);
            }

            public override bool TryOnNext(T element)
            {
                return actual.TryOnNext(element);
            }

            public override bool Poll(out T item)
            {
                if (qs.Poll(out item))
                {
                    return true;
                }

                if (fusionMode == FusionSupport.SYNC)
                {
                    if (Interlocked.CompareExchange(ref once, 1, 0) == 0)
                    {
                        // no need for try-catch as Poll is expected to throw
                        resourceCleanup?.Invoke(resource);
                    }
                }
                return false;
            }

            protected override void OnStart(ISubscription subscription)
            {
                actual.OnSubscribe(this);
            }
            public override void Cancel()
            {
                if (eager)
                {
                    if (Interlocked.CompareExchange(ref once, 1, 0) == 0)
                    {
                        try
                        {
                            resourceCleanup?.Invoke(resource);
                        }
                        catch
                        {
                            // what to do?
                        }
                    }
                }
                base.Cancel();
                if (!eager)
                {
                    DisposeAfter();
                }
            }

            void DisposeAfter()
            {
                if (Interlocked.CompareExchange(ref once, 1, 0) == 0)
                {
                    try
                    {
                        resourceCleanup?.Invoke(resource);
                    }
                    catch
                    {
                        // TODO what should happen with this?
                    }
                }
            }
        }
    }
}
