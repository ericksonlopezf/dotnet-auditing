# ADR-001: Compile-Time Dispatch via Source Generator

**Status**: Accepted

### Context
Traditional mediators (MediatR, Brighter, MassTransit) rely on runtime reflection, assembly scanning, and dynamic invocation to route requests to handlers. This approach is inherently hostile to Native AOT compilation and IL trimming.

### Problem
How do we route `ICommand<TResponse>` to its handler without runtime reflection, without dictionary lookup on `Type` objects, and without heap allocations from delegate creation?

### Options Considered
1. `Dictionary<Type, Func<object, CancellationToken, Task<object>>>` — reflection at lookup, boxing
2. `Dictionary<Type, object>` + `MethodInfo.Invoke` — reflection at dispatch, heap alloc
3. Pattern-matching switch on boxed object — boxing for structs, pattern match overhead
4. **Roslyn Incremental Generator + generated switch** — zero reflection, zero boxing for class types

### Decision
Use `IIncrementalGenerator` to discover all handler implementations at compile time and generate a concrete `GeneratedMediator` class with explicit `switch` statements per request type.

The generator — not the runtime — owns the Request → Handler mapping.

### Why
- `GetRequiredService<TConcreteHandler>()` is AOT-safe (concrete type known at compile time)
- `Unsafe.As<ValueTask<TResponse>, ValueTask<TResponse>>` avoids boxing (types verified by generator)
- The switch statement is compiled to efficient jump tables or cascaded comparisons by the JIT/AOT

### Consequences
+ Zero reflection at runtime
+ Full Native AOT compatibility
+ Compile-time handler discovery errors (ELM001)
+ Startup: O(1) vs O(n) assembly scanning
- Requires generator infrastructure (maintenance cost)
- Generated code can be large with many handlers (acceptable)
- Cannot register handlers dynamically at runtime (by design)

### Rejected Alternatives
- Runtime DI scanning: `services.AddHandlersFromAssembly()` — incompatible with trimming
- `GetService(typeof(ICommandHandler<TCommand, TResponse>))` — dynamic type, AOT-unsafe

---

