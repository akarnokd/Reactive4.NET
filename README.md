# Reactive4.NET
<a href='https://www.nuget.org/packages/Reactive4.NET'><img src='https://img.shields.io/nuget/v/Reactive4.NET.svg' alt="Reactive4.NET NuGet version"/></a>

Modern, [Reactive-Streams](https://github.com/reactive-streams/reactive-streams-dotnet) compliant library for composing (a)synchronous sequences of data and events.

Supports:

- .NET Framework 4.5 or newer
- NETStandard 1.6 or newer

# Getting started

## Installation

From NuGet console:

```
nuget> Install-Package Reactive4.NET
```

or search for `Reactive4.NET` and install the desired version.

## Hello World

The library uses the `Reactive4.NET` namespace.

```cs
using Reactive4.NET;

namespace demo {
    public class Program1 
    {
        public static void Main() 
        {
            Flowable.Just("Hello World!").Subscribe(Console.WriteLine);
        }
    }
}
```

Standard `IFlowable` operators are defined as extension methods in the `Flowable` class.

### Reactive-Streams

The library defines the `IFlowable` interface as the base interface that extends the standard Reactive-Streams `IPublisher` in the `Reactive.Streams` namespace.

```cs
using Reactive4.NET;
using Reactive.Streams;

namespace demo {
    public class Program2 {
        public static void Main() 
        {
            IPublisher<int> publisher = Flowable.Range(1, 5);
            ISubscriber<int> subscriber = new ConsoleSubscriber();
            
            publisher.Subscribe(subscriber);
        }
        
        sealed class ConsoleSubscriber : ISubscriber<int> 
        {
            ISubscription upstream;
            
            public void OnSubscribe(ISubscription s) 
            {
                upstream = s;
                s.Request(1);
            }
            
            public void OnNext(int element) 
            {
                Console.WriteLine(element);
                upstream.Request(1);
            }
            
            public void OnError(Exception cause) 
            {
                Console.WriteLine(cause);
            }
            
            public void OnComplete() 
            {
                Console.WriteLine("Done");
            }
        }
    }
}
```

### Parallel extensions

In addition, Reactive4.NET provides a the parallel extension that allows parallel processing of `IFlowable` sequences:

```cs
using Reactive4.NET;

namespace demo {
    public class Program3 {
        public static void Main() 
        {
            Flowable.Range(1, 10)
            .Parallel()
            .RunOn(Executors.Computation)
            .Map(v => v * v)
            .Sequential()
            .SumInt()
            .Test()
            .AssertResult(1 + 4 + 9 + 16 + 25 + 36 + 49 + 64 + 81);
        }
    }
}
```

The parallel operators are defined as extension methods in the `ParallelFlowable` class.

It is possible to create a parallel flow from individual `IPublisher`s via the `ParallelFlowable.FromArray()` method.

```cs
using Reactive4.NET;

namespace demo {
    public class Program4 {
        public static void Main() 
        {
            ParalleFlowable(
                Flowable.Range(1, 5), 
                Flowable.Range(6, 5), 
                Flowable.Range(11, 5))
            .RunOn(Executors.Computation)
            .Map(v => v * v)
            .Sequential()
            .SumInt()
            .Test()
            .AssertResult(1 + 4 + 9 + 16 + 25 + 36 + 49 + 64 + 81);
        }
    }
}
```


## Creating `IFlowable`s

TBD

## Manipulating `IFlowable`s

TBD

## Consuming `IFlowable`s

TBD

# Contact

- GitHub: [Issue list](https://github.com/akarnokd/Reactive4.NET/issues).
- Twitter: [@akarnokd](https://twitter.com/akarnokd)
