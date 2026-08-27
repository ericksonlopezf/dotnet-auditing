# ADR-025: Struct-Based Zero-Allocation Notification Behaviors via INext

**Status**: Accepted (Implemented in v1.0)

### Context
Previous mediator implementations executed notification pipelines using closures or delegates (`NotificationHandlerDelegate`), which caused heap allocations on every event publication. For high-throughput systems, event publishing must be allocation-free on synchronous paths.

### Decision
Define `INotificationBehavior<TNotification>` using a generic struct constraint `where TNext : struct, INext`. The Roslyn source generator generates specialized `internal readonly struct` continuations for notification handler invocation and behavior chains.

### Consequences
- Eliminates delegate heap allocations during notification dispatch.
- Aligns `INotificationBehavior` with `IPipelineBehavior<TRequest, TResponse>`.
- Fully compatible with Native AOT without closure capture overhead.
