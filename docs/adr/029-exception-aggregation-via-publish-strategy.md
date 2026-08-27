# ADR-029: Exception Aggregation via PublishStrategy.SequentialAggregateExceptions

**Status**: Accepted (Implemented in v1.0)

### Context
When publishing domain notifications to multiple handlers, a failure in the first handler normally halts subsequent handler execution. In event-driven domains, callers often need all handlers to execute regardless of intermediate failures and receive an aggregated exception report.

Previous designs required manual handler list passing (`PublishWithAggregation`), exposing internal dispatching details.

### Decision
Introduce `PublishStrategy.SequentialAggregateExceptions` in `[PublishStrategyAttribute]`. When present on a notification type, the Roslyn generator emits sequential execution within `try/catch` blocks, accumulating exceptions and throwing `NotificationHandlerAggregateException` after all handlers execute.

### Consequences
- Callers use standard `publisher.Publish(notification)` without needing handler references.
- Exception aggregation is declaratively configured per notification type.
