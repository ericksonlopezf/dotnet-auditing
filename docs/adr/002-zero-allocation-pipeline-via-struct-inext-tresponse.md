# ADR-002: Zero-Allocation Pipeline via Struct INext<TResponse>

**Status**: Accepted

### Context
MediatR's pipeline uses `RequestHandlerDelegate<TResponse>` (a delegate) to represent the "next" step. Each delegate created inside a `foreach` loop captures variables (closures), causing heap allocations on every request.

### Problem
How do we build a composable pipeline that avoids heap allocations caused by closure captures and delegate creation?

### Options Considered
1. `Func<CancellationToken, Task<T>>` delegate — heap allocation per behavior
2. `RequestHandlerDelegate<T>` (MediatR style) — same problem
3. Class-based INext — heap allocation for the class instance
4. **Struct-based INext with generic constraint** — stack-allocated, no boxing

### Decision
Define:
```csharp
public interface INext<TResponse> { ValueTask<TResponse> InvokeAsync(); }
public interface IPipelineBehavior<in TRequest, TResponse>
{
    ValueTask<TResponse> Handle<TNext>(TRequest request, TNext next, CancellationToken ct)
        where TNext : struct, INext<TResponse>;
}
```

The source generator produces `readonly struct` implementations for each behavior transition and the final handler invocation. These structs are allocated on the stack (or inlined by JIT/AOT).

### Why
- `where TNext : struct` prevents boxing by the generic constraint
- The struct fields hold references to already-allocated handler/behavior objects
- On the synchronous completion path of `ValueTask`, no heap allocations occur
- JIT/AOT can inline/devirtualize the struct method calls

### Consequences
+ Zero allocations per behavior hop (sync-completion path)
+ JIT/AOT friendly (devirtualization of struct calls)
+ Highly optimizable pipeline
- API is slightly unfamiliar (vs MediatR's `Func<Task<T>>`)
- Behavior implementations must add `where TNext : struct, INext<TResponse>` generic constraint
- Generated struct names can be verbose

### Rejected Alternatives
- Linked list of closures (MediatR style) — O(n) allocations per request
- Class-based continuation — at least one heap alloc per hop

---

