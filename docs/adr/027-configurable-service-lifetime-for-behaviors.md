# ADR-027: Configurable [ServiceLifetime] on Pipeline Behaviors

**Status**: Accepted (Implemented in v1.0)

### Context
Pipeline behaviors are often stateless (e.g. logging, metrics, tracing) and should be registered as Singletons to eliminate allocation on each request. Conversely, behaviors needing database contexts (e.g. validation, transaction boundaries) must be Scoped. Hardcoding all behaviors as Transient was inefficient.

### Decision
`DependencyInjectionGenerator` and `MediatorModelBuilder` read `[ServiceLifetime]` from behavior types. If present, behaviors are registered as `Singleton` or `Scoped`; otherwise, they default to `Transient`.

### Consequences
- Stateless behaviors registered with `[ServiceLifetime(HandlerLifetime.Singleton)]` have zero DI resolution overhead across requests.
- Scoped behaviors integrate seamlessly with `IServiceScope`.
