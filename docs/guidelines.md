# Engineering Guidelines & CQRS Standards

## 1. Core Directives
1. **Segregate Mutating Commands from Side-Effect-Free Queries**: Always implement `ICommand<TResponse>` for state modifications and `IQuery<TResponse>` for idempotent reads.
2. **Immutable Request Types**: Declare all commands, queries, and domain events as `public readonly record struct` or `sealed record` with `init`-only properties.
3. **Never Block on Asynchronous Handlers**: Always return `ValueTask<TResponse>` and propagate `cancellationToken` down the execution tree.
4. **Use Struct Behaviors for Zero Allocations**: Constrain pipeline continuations with `where TNext : struct, INext<TResponse>`.
5. **Enforce Zero Allocations in Unit Tests**: Use `MediatorContractExtensions.AssertZeroAllocations` in pipeline test assertions.
