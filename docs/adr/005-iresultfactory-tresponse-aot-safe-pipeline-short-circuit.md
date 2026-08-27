# ADR-005: IResultFactory<TResponse> — AOT-Safe Pipeline Short-Circuit

**Status**: Accepted

### Context
Pipeline behaviors often need to short-circuit execution and return a failure response without throwing exceptions (which are expensive). In a reflection-based world, a behavior could create a `Result.Failure()` via reflection. In AOT, this is forbidden.

### Decision
Define `IResultFactory<out TResponse>` as an interface that behaviors can inject. The source generator automatically generates a concrete implementation for any response type that is a `Result<T>` (from `EricksonLopez.Result`).

### Why
- Behaviors don't need to know the concrete response type at authoring time
- The generator produces the factory implementation in compile time — no reflection
- The factory is registered as Singleton (stateless, no overhead)
- Short-circuiting without exceptions avoids StackTrace allocation overhead

### Consequences
+ AOT-safe failure short-circuit in behaviors
+ No reflection for `Result.Failure()` creation
+ Zero runtime cost when the generator produces the factory
- Soft dependency on `EricksonLopez.Result` semantics (result must have `Failure(Error)` static method)
- Only works for response types that follow the Result pattern

### Reconsideration Criteria
If `EricksonLopez.Result` API changes, `IResultFactory<T>` contract must be reviewed.

---

