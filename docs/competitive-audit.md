# Competitive Audit & Technical Comparison

## 1. Architectural Differences vs MediatR

| Architectural Aspect | MediatR 12.x | EricksonLopez.Mediator |
|---|---|---|
| **Handler Discovery** | Runtime `services.AddMediatR(...)` assembly reflection scanning. | Compile-time incremental Roslyn generator (`AddMediator()`). |
| **Pipeline Delegate** | `RequestHandlerDelegate<TResponse>` allocating heap closures. | Struct `INext<TResponse>` with zero heap allocation. |
| **Return Types** | Mandatory `Task<TResponse>` causing task object allocations. | Native `ValueTask<TResponse>` eliminating allocations on synchronous paths. |
| **Type Safety** | Generic `IRequest<T>` conflating reads and writes. | Strict separation: `ICommand<T>` vs `IQuery<T>`. |
| **Error Handling** | Relies on throwing and catching exceptions through the pipeline. | Seamless integration with functional `Result<T>` via `IResultFactory<T>`. |
