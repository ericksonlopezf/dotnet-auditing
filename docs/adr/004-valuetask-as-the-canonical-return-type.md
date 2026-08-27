# ADR-004: ValueTask as the Canonical Return Type

**Status**: Accepted

### Context
`Task<T>` always allocates a heap object. `ValueTask<T>` can complete synchronously without allocation when the result is immediately available.

### Decision
All public API returns `ValueTask<T>` or `ValueTask`. Handlers must implement `ValueTask<T>`.

### Why
- Handler operations (especially when using Result pattern) can complete synchronously without I/O
- In synchronous completion paths (validation failures, cached results), `ValueTask` allocates 0 bytes
- `ValueTask` is the standard for high-performance .NET async APIs since .NET 5

### Consequences
+ Zero allocs on synchronous paths
+ Consistent with high-performance .NET patterns
- Developers cannot return `Task<T>` directly (must wrap: `return ValueTask.FromResult(...)`)
- Slightly more friction for developers used to Task-based APIs

### Rejected Alternatives
- `Task<T>` — always allocates, lower performance on sync paths
- Both `Task<T>` and `ValueTask<T>` — API surface complexity

---

