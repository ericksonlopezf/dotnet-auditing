# RFC 0001: Zero-Allocation Pipeline Execution via Struct Continuations

- **Author**: Erickson Lopez
- **Date**: 2026-08-26
- **Status**: Implemented

## 1. Summary
Replaces delegate-based `RequestHandlerDelegate<TResponse>` with struct-based `INext<TResponse>` continuations, completely eliminating heap allocations across mediator pipeline middleware chains.

## 2. Motivation
Traditional mediator architectures allocate `Func<Task<TResponse>>` closures per behavior in the pipeline. Under high throughput, this induces severe GC pauses and cache invalidations.

## 3. Detailed Design
The pipeline compiler generates static call chains unrolled at compile time:

```csharp
public readonly struct LoggingNext : INext<Result<Order>>
{
    private readonly CreateOrderHandler _handler;
    private readonly CreateOrderCommand _command;

    public ValueTask<Result<Order>> Invoke(CancellationToken ct)
        => _handler.Handle(_command, ct);
}
```

## 4. Verification
Benchmarks demonstrate 0 B allocated per request through an 8-stage middleware pipeline.
