# ADR-003: ICommand<T> and IQuery<T> Separation (CQRS Semantic Enforcement)

**Status**: Accepted

### Context
MediatR uses `IRequest<TResponse>` as a unified marker for both commands and queries. This means a mutation-intensive command and a read-only query are type-compatible, losing CQRS semantic enforcement.

### Problem
Should we enforce the Command/Query separation at the type system level?

### Options Considered
1. `IRequest<TResponse>` unified (MediatR style) — no CQRS enforcement
2. **`ICommand<TResponse>` + `IQuery<TResponse>`** — CQRS enforced at compile time
3. Convention-based naming (Wolverine style) — implicit, no interfaces
4. Marker attributes — not enforced by type system

### Decision
`ICommand<TResponse>` and `IQuery<TResponse>` are separate, non-interchangeable marker interfaces. The `IMediator` interface has separate `Send(ICommand<T>)` and `Send(IQuery<T>)` overloads. The generator tracks them separately and validates handlers separately.

### Why
- Commands and queries have fundamentally different semantics (mutation vs. read-only)
- Type system enforcement prevents `mediator.Send(query)` where a command is expected
- CQRS is explicit in code, not a convention to be documented
- Separate dispatch paths allow future optimization (e.g., query-specific caching, command-specific audit)

### Consequences
+ Compile-time CQRS enforcement
+ Clearer intent in code
+ Separate handler contracts (`ICommandHandler` vs `IQueryHandler`)
- Two interfaces instead of one (slightly more verbose)
- Developers cannot treat commands and queries uniformly (by design)

### Reconsideration Criteria
If strong demand exists for a `IRequest<T>` unified interface (for migration compat), add it as OPT in v1.1 without removing ICommand/IQuery.

---

