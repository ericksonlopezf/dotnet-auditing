# ADR-030: Rejection of Duplicate In-Process CQRS Dispatchers (CommandBus / QueryBus)

**Status**: Accepted — August 2026

### Context
In microservice and modular monolith architectures, teams occasionally propose introducing secondary in-process dispatchers such as a dedicated `ICommandBus` or `IQueryBus` alongside `IMediator`.

An ecosystem architecture audit evaluated whether splitting in-process dispatching across multiple abstractions provides value or causes architectural fragmentation.

### Decision
Strictly **reject** introducing competing in-process command/query bus implementations across the ecosystem:
1. `EricksonLopez.Mediator` is the **sole capability owner** of in-process request/response (`ICommand<T>`, `IQuery<T>`), pipeline execution, and in-process notification dispatching.
2. Segregation of commands vs queries is enforced semantically at compile time via marker interfaces (`ICommand<TResponse>` vs `IQuery<TResponse>`) rather than by maintaining duplicate runtime dispatching infrastructure.
3. Networked message transport concerns belong exclusively to `EricksonLopez.Messaging` and must never be conflated with in-process mediation.

### Consequences
- **Positive**: Single, unified pipeline configuration for validation, logging, tracing, resilience, and metrics across all in-process operations.
- **Positive**: Zero confusion regarding which dispatching mechanism to inject into domain services or presentation controllers.
- **Enforcement**: No secondary `CommandBus` or `QueryBus` package may be created in the `EricksonLopez.*` ecosystem.
