# ADR-026: Decoupling Result Pattern into EricksonLopez.Mediator.Result

**Status**: Accepted (Implemented in v1.0)

### Context
Directly referencing `EricksonLopez.Result` inside `EricksonLopez.Mediator` core package forced all mediator users to transitively depend on the result library, violating core dependency purity.

### Decision
Extract `IResultFactory<TResponse>` and Result-specific abstractions into a dedicated standalone package `EricksonLopez.Mediator.Result`. The source generator detects response types from `EricksonLopez.Result` and emits typed `ResultFactory` implementations registered into DI conditionally.

### Consequences
- `EricksonLopez.Mediator` core package has zero external runtime dependencies.
- Applications adopting the Result pattern can reference `EricksonLopez.Mediator.Result` to enable short-circuiting behaviors without reflection.
