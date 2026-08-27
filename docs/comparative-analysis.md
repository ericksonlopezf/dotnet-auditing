# Comparative Analysis — EricksonLopez.Mediator vs Ecosystem

This document objectively analyzes `EricksonLopez.Mediator` compared to `MediatR` and `Wolverine` across critical architectural and performance dimensions.

---

## Comparison Matrix

| Capability / Feature | EricksonLopez.Mediator | MediatR 12.x | Wolverine |
|---|---|---|---|
| **Dispatch Mechanism** | Compile-Time Static Switch | Runtime Reflection & Dynamic Delegates | Runtime Code Generation (Roslyn compilation at startup) |
| **Pipeline Invocations** | Zero-Allocation Struct `INext<T>` | Heap Allocated Closures (`RequestHandlerDelegate`) | Generated Code Invokers |
| **Native AOT Compliance** | **100% Native AOT Compatible** (0 warnings) | Partial (Relies on MakeGenericType / reflection) | Incompatible with Native AOT (Emits dynamic assembly) |
| **Compile-Time Safety** | Diagnostics (`ELM001` - `ELM011`) | Runtime `InvalidOperationException` | Startup failure |
| **Startup Overhead** | **0 ms** (pre-generated at build) | Medium (scanning assemblies via reflection) | High (compiles dynamic code at runtime) |
| **Multi-Targeting** | `.NET 8.0`, `.NET 9.0`, `.NET 10.0` | `.NET Standard 2.0+`, `.NET 6.0+` | `.NET 8.0+` |
| **Domain Events Support** | Native Sequential / Parallel / Aggregated | Sequential / Parallel | Advanced Messaging |
| **Result Pattern Support** | First-Class via `IResultFactory<T>` | None (Custom behaviors required) | Built-in |
| **Observability** | Native OpenTelemetry Meter + Activity | Third-party packages | Built-in OpenTelemetry |

---

## Deep Dive: Why Zero-Allocation Struct Pipelines Matter

In traditional MediatR:
```csharp
// MediatR pipeline continuation allocates a delegate object on every request:
public delegate Task<TResponse> RequestHandlerDelegate<TResponse>();
```
Whenever a request traverses 1 or more behaviors in MediatR, closures are captured on the managed heap, increasing GC Gen0 pressure significantly in high-throughput APIs (thousands of requests/sec).

In `EricksonLopez.Mediator`:
```csharp
// Unboxed struct chain - zero heap allocation:
public interface IPipelineBehavior<in TRequest, TResponse>
{
    ValueTask<TResponse> Handle<TNext>(TRequest request, TNext next, CancellationToken cancellationToken)
        where TNext : struct, INext<TResponse>;
}
```
The Source Generator constructs a chain of `internal readonly struct` types, completely avoiding GC heap allocations on the synchronous execution path.
