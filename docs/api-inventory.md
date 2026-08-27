# Complete Public API Surface Inventory

## 1. Core Abstractions (`EricksonLopez.Mediator`)

| Type | Kind | Responsibility |
|---|---|---|
| `ISender` | Interface | Point-to-point dispatch contract for `ICommand`, `IQuery`, and `IStreamQuery`. |
| `IPublisher` | Interface | Multi-subscriber publish contract for `INotification`. |
| `IMediator` | Interface | Unified interface implementing both `ISender` and `IPublisher`. |
| `ICommand<out TResponse>` | Interface | Marker for state-altering command requests. |
| `IQuery<out TResponse>` | Interface | Marker for side-effect-free data queries. |
| `IStreamQuery<out TResponse>` | Interface | Marker for asynchronous streaming queries. |
| `INotification` | Interface | Marker for multi-subscriber domain events. |
| `ICommandHandler<in TCommand, TResponse>` | Interface | Handler contract returning `ValueTask<TResponse>`. |
| `IQueryHandler<in TQuery, TResponse>` | Interface | Handler contract returning `ValueTask<TResponse>`. |
| `IStreamQueryHandler<in TQuery, out TResponse>` | Interface | Handler contract returning `IAsyncEnumerable<TResponse>`. |
| `INotificationHandler<in TNotification>` | Interface | Handler contract returning `ValueTask`. |
| `IPipelineBehavior<TRequest, TResponse>` | Interface | Middleware behavior intercepting request pipelines via struct `INext<TResponse>`. |
| `INotificationBehavior<TNotification>` | Interface | Middleware behavior intercepting notification publishing via struct `INext`. |
| `INext<TResponse>` | Interface | Zero-allocation struct continuation interface for requests. |
| `INext` | Interface | Zero-allocation struct continuation interface for notifications. |
| `IResultFactory<TResponse>` | Interface | Factory contract for generating failure responses without throwing exceptions. |
| `[UseBehaviorAttribute]` | Attribute | Decorates requests to bind dedicated pipeline behaviors with order indices. |
| `[UseGlobalBehaviorAttribute]` | Attribute | Assembly-level attribute binding global behaviors across all requests. |
| `[ServiceLifetimeAttribute]` | Attribute | Configures handler/behavior DI lifetime (`Transient`, `Scoped`, `Singleton`). |
| `[PublishStrategyAttribute]` | Attribute | Configures notification dispatch strategy (`Sequential`, `Parallel`, `SequentialAggregateExceptions`). |
| `[DiscoverHandlersAttribute]` | Attribute | Directs Roslyn generator to include external referenced assemblies in handler discovery. |
