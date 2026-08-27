# Public API Surface Declaration

## 1. Core Namespace: `EricksonLopez.Mediator`
- `ISender`
- `IPublisher`
- `IMediator`
- `ICommand<out TResponse>`
- `IQuery<out TResponse>`
- `IStreamQuery<out TResponse>`
- `INotification`
- `ICommandHandler<in TCommand, TResponse>`
- `IQueryHandler<in TQuery, TResponse>`
- `IStreamQueryHandler<in TQuery, out TResponse>`
- `INotificationHandler<in TNotification>`
- `IPipelineBehavior<TRequest, TResponse>`
- `INotificationBehavior<TNotification>`
- `INext<TResponse>`
- `INext`
- `IResultFactory<TResponse>`
- `[UseBehaviorAttribute]`
- `[UseGlobalBehaviorAttribute]`
- `[ServiceLifetimeAttribute]`
- `[PublishStrategyAttribute]`
- `[DiscoverHandlersAttribute]`
