# ADR-015: Experimental IAsyncEnumerable<T> Streaming via [Experimental("ELM_STREAMING")]

**Status**: ~~Accepted (Implemented as Opt-in Experimental in v1.0)~~ **Superseded by [ADR-034](034-promotion-streaming-stable-direct-handler-dispatch.md)**

> [!NOTE]
> This ADR is retained as historical record. The `[Experimental("ELM_STREAMING")]` attribute was removed and streaming was promoted to Stable in ADR-034. The implementation described here is no longer accurate — `IStreamRequest<T>` and `IStreamRequestHandler<T,T>` are public stable APIs with no `[Experimental]` annotation.

### Context
Streaming operations using `IAsyncEnumerable<T>` allow chunked, asynchronous item retrieval. However, streaming pipelines differ fundamentally from single-response request/response pipelines:
1. They cannot easily compose through zero-allocation `INext<TResponse>` structs without boxing or state-machine allocations.
2. Trimming and Native AOT code generation for generic async state machines require careful suppression.

### Decision (Historical — superseded)
Include `IStreamRequest<TResponse>`, `IStreamRequestHandler<TRequest, TResponse>`, and `ISender.CreateStream<TResponse>()` in v1.0 marked with `[Experimental("ELM_STREAMING")]`.

### Consequences (Historical — superseded)
- Consumers could opt in to streaming by suppressing diagnostic `ELM_STREAMING`.
- Standard CQRS commands and queries remain 100% warning-free under Native AOT.
- Pipeline behaviors (`IPipelineBehavior`) are intentionally not executed for streams in v1.0 to preserve struct performance guarantees.
- Full stream pipeline behaviors (`IStreamPipelineBehavior`) are targeted for v2.0.

### Supersession Note
Per ADR-034: the `[Experimental]` attribute was removed. Streaming is Stable in v1.0 GA. The `ELM_STREAMING` diagnostic no longer applies.
