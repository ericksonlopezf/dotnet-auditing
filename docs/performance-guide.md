# Performance & Zero-Allocation Guide — EricksonLopez.Mediator

This guide explains how to achieve maximum throughput, sub-nanosecond dispatch latency, and zero heap allocations using `EricksonLopez.Mediator`.

---

## 1. Struct-Based Pipeline Execution (`INext<TResponse>`)

Traditional pipelines use delegate closures (`Func<Task<TResponse>>` or `RequestHandlerDelegate`), allocating 48–128 bytes on the managed heap per request.

`EricksonLopez.Mediator` compiles unboxed struct continuations:

```csharp
public class TimingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
{
    public async ValueTask<TResponse> Handle<TNext>(TRequest request, TNext next, CancellationToken cancellationToken)
        where TNext : struct, INext<TResponse> // Zero allocation: unboxed struct passed by value
    {
        // ...
        return await next.InvokeAsync().ConfigureAwait(false);
    }
}
```

### Best Practices:
- Always constrain `TNext` with `where TNext : struct, INext<TResponse>` (or `where TNext : struct, INext` for notifications).
- Avoid boxing `next` into an interface variable inside the behavior.

---

## 2. Handler & Behavior Lifetimes (`[ServiceLifetime]`)

By default, handlers and behaviors are registered as `Transient` in Microsoft.Extensions.DependencyInjection.

```csharp
// Stateless handlers: Register as Singleton to eliminate DI instantiation overhead
[ServiceLifetime(HandlerLifetime.Singleton)]
public class FastCalculationHandler : ICommandHandler<CalculateCommand, int>
{
    public ValueTask<int> Handle(CalculateCommand command, CancellationToken cancellationToken)
        => new(command.X + command.Y);
}
```

| Lifetime | When to Use | Memory Overhead |
|---|---|---|
| `Singleton` | Stateless handlers, pure computation, static caches, logging behaviors | **0 B** per request |
| `Scoped` | Handlers injecting `DbContext`, unit of work, tenant context | Managed by `IServiceScope` |
| `Transient` | Handlers with per-request transient state | Instantiated per dispatch |

---

## 3. Prefer `ValueTask<T>` Over `Task<T>`

All mediator handlers return `ValueTask<TResponse>`:
- If a result is immediately available or cached: `return ValueTask.FromResult(cachedValue);` or `return new ValueTask<TResponse>(value);`
- This avoids allocating a `Task<T>` reference object on the managed heap.

---

## 4. Native AOT Compilation

`EricksonLopez.Mediator` contains **0 runtime reflection calls**, making it 100% compatible with Native AOT publishing:

```xml
<PropertyGroup>
  <PublishAot>true</PublishAot>
</PropertyGroup>
```

When building under Native AOT:
- Source generator dispatch switches are compiled directly to native machine code jumps (`jmp` / `switch`).
- Struct pipeline invocations are fully inlined by RyuJIT.
- Startup time is instantaneous (0 assembly scanning, 0 runtime JIT compilation).
