# ADR-022: Segregated ISender and IPublisher Interfaces in v1.0

**Status**: Accepted (Implemented in v1.0)

### Context
In Clean Architecture and CQRS applications, consumers often only need to send commands/queries (`ISender`) or only publish domain events (`IPublisher`). Injecting a monolithic `IMediator` violates the Interface Segregation Principle (ISP) and complicates mocking in unit tests.

### Decision
`IMediator` inherits from both `ISender` and `IPublisher`. The source generator automatically registers all three interfaces (`IMediator`, `ISender`, and `IPublisher`) pointing to the generated compile-time dispatcher `GeneratedMediator`.

### Consequences
- Consumers can inject `ISender` when only dispatching requests.
- Consumers can inject `IPublisher` when only publishing notifications/events.
- Zero extra allocations or complexity at runtime.
