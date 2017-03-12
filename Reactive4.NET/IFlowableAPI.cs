using System;
using System.Collections.Generic;
using System.Text;
using Reactive.Streams;

namespace Reactive4.NET
{
    public interface IFlowableSubscriber<in T> : ISubscriber<T>
    {
    }

    public interface IConditionalSubscriber<in T> : IFlowableSubscriber<T>
    {
        bool TryOnNext(T item);
    }

    public interface IVariableSource<T>
    {
        bool Value(out T value);
    }

    public interface IConstantSource<T> : IVariableSource<T>
    {
    }


    public interface ISimpleQueue<T>
    {
        bool Offer(T item);

        bool Poll(out T item);

        bool IsEmpty();

        void Clear();
    }

    public interface IQueueSubscription<T> : ISubscription, ISimpleQueue<T>
    {
        int RequestFusion(int mode);
    }

    public interface IFlowable<out T> : IPublisher<T>
    {
        void Subscribe(IFlowableSubscriber<T> subscriber);
    }

    public interface IHasSource<T>
    {
        IFlowable<T> Source { get; }
    }

    public interface IReplaceSource<T>
    {
        IFlowable<T> WithSource(IFlowable<T> newSource);
    }

    public interface IFlowableProcessor<T> : IFlowable<T>, IProcessor<T, T>, IFlowableSubscriber<T>, IDisposable
    {
        bool HasComplete { get; }

        bool HasException { get; }

        Exception Exception { get; }

        bool HasSubscribers { get; }
    }

    public static class FusionSupport
    {
        public static readonly int NONE = 0;
        public static readonly int SYNC = 1;
        public static readonly int ASYNC = 2;
        public static readonly int ANY = SYNC | ASYNC;
        public static readonly int BARRIER = 4;
    }

    public interface IGroupedFlowable<K, V> : IFlowable<V>
    {
        K Key { get; }
    }

    public interface IConnectableFlowable<T> : IFlowable<T>
    {
        IDisposable Connect(Action<IDisposable> onConnect = null);
    }
}
