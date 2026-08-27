# Level 07: Performance & Native AOT

`EricksonLopez.Mediator` was engineered from the ground up to achieve maximum throughput, zero runtime allocations, and 100% Native AOT compatibility.

---

## 1. The Bottlenecks of Legacy Mediators

Traditional mediator libraries rely heavily on runtime reflection (`Type.MakeGenericType`, `Activator.CreateInstance`, and runtime assembly scanning):
1. **Throughput Penalty:** Dynamic invocation and delegate allocations cause GC pressure and latency spikes.
2. **Native AOT Trimming Warnings:** The Native AOT compiler cannot statically analyze types instantiated dynamically via string or open generic reflection, resulting in trim breaks.

---

## 2. Compile-Time Monomorphization

`EricksonLopez.Mediator.Generator` intercepts your syntax trees during compilation and generates a hard-coded, strongly-typed type-switch dispatch table (`GeneratedMediator`):

```csharp
// Source-generated dispatch method (conceptual excerpt)
public ValueTask<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken ct)
{
    return request switch
    {
        CreateUserCommand cmd => (ValueTask<TResponse>)(object)DispatchCommand(cmd, ct),
        GetUserByIdQuery query => (ValueTask<TResponse>)(object)DispatchQuery(query, ct),
        _ => throw new InvalidOperationException($"No handler registered for {request.GetType().FullName}")
    };
}
```

This guarantees:
- **0 runtime reflection calls**
- **0 IL trimming warnings** (`EnableTrimAnalyzer=true`, `IsAotCompatible=true`)
- Direct method inlining opportunities by the RyuJIT / NativeAOT compilers

---

## 3. Zero Heap Allocations via Struct Continuations

Middleware pipelines in `EricksonLopez.Mediator` are modeled as nested value types (`struct INext<TResponse>`):
- Continuations reside directly on the execution stack.
- No `Func<Task<T>>` closures or delegate arrays are allocated on the managed heap.
- `ValueTask<TResponse>` prevents task allocations when handlers or short-circuits return synchronously.

---

## 4. `StaticMediator` (Direct Zero-DI Dispatch)

For ultra-high-performance pathways where dependency injection overhead must be eliminated entirely, `StaticMediator` provides static direct dispatch without `IServiceProvider` lookups.
