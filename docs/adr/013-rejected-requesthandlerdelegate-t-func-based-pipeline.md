# ADR-013: REJECTED — RequestHandlerDelegate<T> (Func-based Pipeline)

**Status**: Accepted (Rejection)

### Context
MediatR uses `public delegate Task<TResponse> RequestHandlerDelegate<TResponse>()` as the "next" parameter in behaviors.

### Decision
**REJECTED**. We use `INext<TResponse>` with struct constraint instead.

### Why
- `RequestHandlerDelegate<T>` requires heap allocation for the delegate object
- Closures that capture variables (handler, request, ct) add additional allocations
- Our struct approach eliminates these allocations on the synchronous path
- The performance difference is measurable in high-throughput scenarios

### Competitive Impact
Zero-allocation pipeline is one of our top 3 differentiators. This rejection is necessary for that claim.

---

