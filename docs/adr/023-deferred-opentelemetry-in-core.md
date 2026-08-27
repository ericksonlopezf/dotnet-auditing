# ADR-023: DEFERRED — OpenTelemetry in Core

**Status**: Deferred to EL.Mediator.OpenTelemetry package

### Context
Should OpenTelemetry tracing and metrics be built into the core mediator?

### Decision
**DEFERRED to separate package**. Core has zero observability overhead.

### Why
- OpenTelemetry is a dependency some projects don't need
- Adding `ActivitySource` to the core adds overhead even when no listener is registered
- The zero-overhead principle requires that disabled observability costs exactly zero
- A `TracingBehavior<TRequest, TResponse>` in a separate package achieves this perfectly

### Reconsideration Criteria
If `ActivitySource` cost when no listener is registered is proven to be zero (it is near-zero already), consider adding to core in v2.0.

---

