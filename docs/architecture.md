# Architecture Overview — EricksonLopez.Mediator

`EricksonLopez.Mediator` is a high-performance, zero-allocation, compile-time mediated CQRS pipeline implementation for .NET 8, 9, and 10.

---

## 1. High-Level Architectural Vision

Unlike traditional mediator implementations in .NET that rely on runtime reflection, runtime DI container resolution per handler, delegate closures, and boxing, `EricksonLopez.Mediator` moves all routing, pipeline weaving, and dependency resolution to **compile time** using **Roslyn Incremental Source Generators**.

```mermaid
graph LR
    subgraph "Compile Time (Roslyn Source Generator)"
        Code["Commands, Queries, Handlers, Behaviors"] --> SG["EricksonLopez.Mediator.Generator"]
        SG --> GM["GeneratedMediator.g.cs"]
        SG --> DI["GeneratedMediatorExtensions.g.cs"]
    end

    subgraph "Runtime Execution (Zero Reflection / Native AOT)"
        Caller["Caller / Controller / Endpoint"] --> ISender["ISender / IMediator"]
        ISender --> GM
        GM --> Pipeline["Struct-based INext<T> Pipeline"]
        Pipeline --> Handler["Concrete Handler Execution"]
    end
```

---

```mermaid
graph TD
    Core["EricksonLopez.Mediator (Core Abstractions)"]
    Generator["EricksonLopez.Mediator.Generator (Roslyn Analyzer/Generator)"] -.->|Weaves & References| Core
    AspNetCore["EricksonLopez.Mediator.AspNetCore (Minimal APIs)"] --> Core
    ResultPkg["EricksonLopez.Mediator.Result (Result Pattern Integration)"] --> Core
    OTel["EricksonLopez.Mediator.OpenTelemetry (Tracing & Metrics)"] --> Core
    Polly["EricksonLopez.Mediator.Polly (Resilience Behaviors)"] --> Core
    RateLimit["EricksonLopez.Mediator.RateLimiting (Rate Limiting)"] --> Core
    Testing["EricksonLopez.Mediator.Testing (FakeMediator Test Double)"] --> Core
    Validation["EricksonLopez.Mediator.Validation (FluentValidation)"] --> Core
    Validation --> ResultPkg
```

| Package | Purpose | Runtime Dependencies | AOT Ready |
|---|---|---|---|
| `EricksonLopez.Mediator` | Core abstractions (`ICommand`, `IQuery`, `INotification`, `IStreamRequest`, `IMediator`, `ISender`, `IPublisher`, `INext`, `StaticMediator`) | `Microsoft.Extensions.DependencyInjection.Abstractions`, `Microsoft.Extensions.Diagnostics.HealthChecks.Abstractions` | ✅ 100% |
| `EricksonLopez.Mediator.Generator` | Incremental Roslyn Source Generator weaving the dispatcher, DI extensions, and diagnostics (`ELM001`–`ELM011`) | Roslyn Analyzer only (development dependency) | N/A |
| `EricksonLopez.Mediator.AspNetCore` | Minimal API endpoint mapping extensions (`MapCommand`, `MapQuery`) | `EricksonLopez.Mediator`, `Microsoft.AspNetCore.App` | ⚠️ Requires AOT config (`[RequiresUnreferencedCode]` on public methods) |
| `EricksonLopez.Mediator.OpenTelemetry` | Distributed tracing (`ActivitySource`) and performance metrics (`Meter`) | `EricksonLopez.Mediator`, `OpenTelemetry.Api` | ✅ 100% (type name caching via closed generic static fields — ADR-030) |
| `EricksonLopez.Mediator.Polly` | Polly v8 resilience policies (retry, circuit breaker, timeout) pipeline behavior | `EricksonLopez.Mediator`, `Polly.Core` | ⚠️ Generally compatible; attribute metadata must be preserved under aggressive trimming |
| `EricksonLopez.Mediator.RateLimiting` | High-throughput rate limiting pipeline behavior | `EricksonLopez.Mediator`, `System.Threading.RateLimiting` | ✅ 100% |
| `EricksonLopez.Mediator.Result` | Type-safe failure result factory (`IResultFactory<TResponse>`) for pipeline short-circuiting | `EricksonLopez.Mediator`, `EricksonLopez.Result` | ✅ 100% |
| `EricksonLopez.Mediator.Testing` | In-memory `FakeMediator` and `DelegateNext` test doubles for unit testing | `EricksonLopez.Mediator` | Test-only |
| `EricksonLopez.Mediator.FluentValidation` | **Recommended** FluentValidation pipeline integration (`ValidationPipelineBehavior<T,R>`, `AddMediatorFluentValidation()`) | `EricksonLopez.Mediator`, `EricksonLopez.Mediator.Result`, `EricksonLopez.Result.FluentValidation` | ⚠️ Behavior itself is AOT-safe; assembly scanning extension uses `[RequiresUnreferencedCode]` |
| `EricksonLopez.Mediator.Validation` | ⚠️ **DEPRECATED (ADR-033)** — archived in v2.0. Use `EricksonLopez.Mediator.FluentValidation` instead. | `EricksonLopez.Mediator`, `EricksonLopez.Mediator.Result`, `EricksonLopez.Result.FluentValidation` | ❌ Not AOT compatible (`[RequiresUnreferencedCode]` on assembly scanning) |

---

## 3. Core Dispatch Architecture

### 3.1 Request / Response Pipeline (Commands & Queries)

When a command (`ICommand<TResponse>`) or query (`IQuery<TResponse>`) is sent via `IMediator.Send`, the dispatch workflow is completely static:

```mermaid
sequenceDiagram
    autonumber
    participant Caller
    participant Mediator as GeneratedMediator (switch dispatch)
    participant Behavior as IPipelineBehavior<TReq, TRes>
    participant Next as readonly struct INext<TRes>
    participant Handler as ICommandHandler / IQueryHandler

    Caller->>Mediator: Send(command, ct)
    Note over Mediator: Direct type-switch matching (C# 9+ pattern matching)
    Mediator->>Behavior: Handle(request, nextStruct, ct)
    Behavior->>Next: InvokeAsync()
    Next->>Handler: Handle(request, ct)
    Handler-->>Next: ValueTask<TResponse>
    Next-->>Behavior: ValueTask<TResponse>
    Behavior-->>Mediator: ValueTask<TResponse>
    Mediator-->>Caller: ValueTask<TResponse>
```

#### Key Performance Mechanics:
1. **Direct Switch Matching**: Dispatches requests via a direct C# type-switch pattern match, avoiding all reflection (`MethodInfo.Invoke` / `MakeGenericMethod`).
2. **Struct Continuations (`INext<TResponse>`)**: The pipeline chain is composed of unboxed `internal readonly struct` instances. Calling `next.InvokeAsync()` is direct and inlinable by RyuJIT and Native AOT compilers, resulting in **0 heap allocations** on synchronous execution paths.
3. **`ValueTask<TResponse>`**: Handlers that return cached or synchronous values avoid `Task` object allocations completely.

---

## 4. Notification & Domain Event Dispatching

Notifications (`INotification`) support multiple subscribers and custom dispatching strategies configured via `[PublishStrategy]`:

```mermaid
graph TD
    Pub["publisher.Publish(notification, ct)"] --> Strategy{PublishStrategy}
    Strategy -->|Sequential (Default)| Seq["Sequential Handler Invocations"]
    Strategy -->|Parallel| Par["Task.WhenAll Concurrency"]
    Strategy -->|SequentialAggregateExceptions| Agg["try / catch per handler -> AggregateException"]
```

### Strategies:
1. **`Sequential` (Default)**: Executes each `INotificationHandler<T>` in sequential order using unboxed `INext` struct continuations.
2. **`Parallel`**: Executes handlers concurrently using `Task.WhenAll`.
3. **`SequentialAggregateExceptions`**: Executes all handlers even if preceding handlers fail, aggregating all thrown exceptions into a `NotificationHandlerAggregateException`.

---

## 5. Architectural Boundaries (What This Library Is NOT)

To maintain extreme performance, simplicity, and architectural purity:

- ❌ **NOT an Outbox / Inbox framework**: Persistent message storage and dual-write guarantees belong in dedicated transactional outbox libraries (e.g. `EricksonLopez.Outbox`).
- ❌ **NOT a Distributed Message Broker**: Network transports (RabbitMQ, Kafka, Azure Service Bus) belong in external messaging frameworks.
- ❌ **NO Runtime Reflection in Core Hot Path**: The `EricksonLopez.Mediator` Core package uses no `Activator.CreateInstance`, `Assembly.GetTypes()`, or `MakeGenericType` at runtime. Note: integration packages (AspNetCore, Polly) use controlled reflection in initialization paths — see [Package Catalog](packages.md) for per-package AOT status.
- ❌ **NO Implicit Magic**: All handlers must explicitly implement `ICommandHandler`, `IQueryHandler`, or `INotificationHandler`.

---

## 6. State Diagram: Request Lifecycle

```mermaid
stateDiagram-v2
    [*] --> Received : ISender.Send() / SendCommand() / SendQuery()
    Received --> Validating : [ValidateRequest] attribute present
    Validating --> ValidationFailed : Constraint violated
    Validating --> Dispatching : All constraints satisfied
    ValidationFailed --> [*] : MediatorValidationException thrown
    Received --> Dispatching : No [ValidateRequest]
    Dispatching --> BehaviorPipeline : Global + Specific behaviors present
    Dispatching --> HandlerExecution : No behaviors registered
    BehaviorPipeline --> HandlerExecution : All behaviors invoked next.InvokeAsync()
    BehaviorPipeline --> ShortCircuited : Behavior returns early (IResultFactory)
    ShortCircuited --> [*] : Failure Result returned (no exception)
    HandlerExecution --> Completed : ValueTask<TResponse> returned
    HandlerExecution --> Failed : Exception thrown
    Completed --> [*] : Success response returned to caller
    Failed --> [*] : Exception propagated to caller
```

---

## 7. State Diagram: Notification Lifecycle

```mermaid
stateDiagram-v2
    [*] --> Published : IPublisher.Publish()
    Published --> StrategySelection : Read [PublishStrategy] attribute
    StrategySelection --> Sequential : PublishStrategy.Sequential (default)
    StrategySelection --> Parallel : PublishStrategy.Parallel
    StrategySelection --> SequentialAgg : PublishStrategy.SequentialAggregateExceptions
    Sequential --> Handler1 : Execute H1
    Handler1 --> Handler2 : Execute H2
    Handler2 --> [*] : All handlers completed (or exception propagated immediately)
    Parallel --> AllHandlersConcurrent : Task.WhenAll
    AllHandlersConcurrent --> [*] : All complete (AggregateException if any fails)
    SequentialAgg --> AllHandlersSeq : Run all, collect exceptions
    AllHandlersSeq --> [*] : NotificationHandlerAggregateException if any failed
```

---

## 8. Error Handling Flow

```mermaid
flowchart TD
    Request([Request enters pipeline]) --> GlobalBehavior[Global IPipelineBehavior]
    GlobalBehavior --> SpecificBehavior[Specific IPipelineBehavior]
    SpecificBehavior --> Validation{ValidateRequest?}
    Validation -->|Yes - Fails| ValidationEx[MediatorValidationException]
    Validation -->|Yes - Passes| Handler[ICommandHandler / IQueryHandler]
    Validation -->|No| Handler
    Handler --> HandlerEx{Exception thrown?}
    HandlerEx -->|No| PollyBehavior{UseResiliencePipeline?}
    HandlerEx -->|Yes - Transient| PollyBehavior
    PollyBehavior -->|Yes - Retries remaining| Handler
    PollyBehavior -->|Yes - Exhausted / No| PropagateEx[Exception propagated to caller]
    SpecificBehavior -->|IResultFactory available| ShortCircuit[Result.Failure returned]
    ShortCircuit --> Caller([Caller receives failure])
    ValidationEx --> Caller
    PropagateEx --> Caller
    Handler -->|Success| ResultValue[ValueTask-TResponse-]
    ResultValue --> Caller
```

---

## 9. Processing Flow: Batch and Streaming

```mermaid
flowchart LR
    BatchCmd([ProcessBatchCommand]) --> Mediator[IMediator.Send]
    Mediator --> BehaviorPipeline[IPipelineBehavior chain]
    BehaviorPipeline --> BatchHandler[ICommandHandler]
    BatchHandler -->|async ItemCount| Response([int: items processed])

    StreamReq([IStreamRequest-T-]) --> Mediator2[ISender.CreateStream]
    Mediator2 --> StreamHandler[IStreamRequestHandler]
    StreamHandler -->|IAsyncEnumerable-T-| Consumer([await foreach item])
    Consumer -->|Back-pressure via CancellationToken| StreamHandler
```

