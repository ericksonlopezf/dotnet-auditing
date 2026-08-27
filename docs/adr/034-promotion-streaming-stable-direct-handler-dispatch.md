# ADR-034: Promotion of IStreamRequest<T> Streaming to Stable via Direct Handler Dispatch

**Status**: Approved (Supersedes ADR-015)

**Date**: 2026-08-26

**Supersedes**: [ADR-015](015-rejected-v1-0-iasyncenumerable-t-streaming.md)

**Superseded by**: —

---

## Context

`IStreamRequest<TResponse>` was originally introduced in v1.0-rc1 as an opt-in experimental feature marked with `[Experimental("ELM_STREAMING")]` (per ADR-015). Consumers were required to suppress diagnostic `ELM_STREAMING` to use streaming without compiler warnings.

Reasons for the initial experimental designation (per ADR-015):
1. Streaming pipelines cannot compose through zero-allocation `INext<TResponse>` structs without boxing or state-machine allocations.
2. Trimming and Native AOT code generation for generic async state machines required investigation.

After implementation and testing in v1.0-rc1:
1. **`[Experimental]` was removed from `IStreamRequest<TResponse>` and `IStreamRequestHandler<TRequest, TResponse>`** — the types are stable and production-ready.
2. The generated `CreateStream<TResponse>()` dispatch uses a direct switch table to the concrete `IStreamRequestHandler<TRequest, TResponse>`, bypassing struct pipeline behaviors (which would require boxing for streaming scenarios).
3. AOT and trimming compatibility has been validated through `EricksonLopez.Mediator.AotTest`.
4. Keeping the `[Experimental]` attribute creates a competitive gap against `MediatR` and `martinothamar/Mediator`, both of which offer stable streaming.

## Decision

Promote `IStreamRequest<TResponse>`, `IStreamRequestHandler<TRequest, TResponse>`, and `ISender.CreateStream<TResponse>()` to **Stable** API status by:
1. Removing the `[Experimental("ELM_STREAMING")]` attribute from all streaming types.
2. Moving streaming types to `PublicAPI.Unshipped.txt` without the `[Experimental]` annotation.
3. Marking ADR-015 as **Superseded by ADR-034**.

### Architectural note on pipeline behaviors with streams:
`IPipelineBehavior<TRequest, TResponse>` is intentionally **not** executed for streaming requests. This is by design to preserve struct performance guarantees — streaming uses `IAsyncEnumerable<TResponse>` state machines which cannot compose with unboxed `INext<TResponse>` structs without boxing. Full stream pipeline behavior support (`IStreamPipelineBehavior`) remains deferred to v2.0.

## Consequences

### Positive
- Streaming is stable and production-ready in v1.0.
- No `ELM_STREAMING` suppression required for consumers.
- Competitive parity with `MediatR` and `martinothamar/Mediator` streaming.
- Clear documented limitation on pipeline behaviors with streams.

### Negative
- Pipeline behaviors do not execute for streaming requests (documented limitation, consistent with ADR-015).
- Full `IStreamPipelineBehavior` remains a v2.0 roadmap item.

### Neutral
- No breaking change for existing users of experimental streaming (the `[Experimental]` attribute removal makes the API more permissive).
- ADR-015 is retained as historical record but marked as Superseded.
