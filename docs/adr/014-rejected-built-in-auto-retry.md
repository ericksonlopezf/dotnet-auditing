# ADR-014: REJECTED — Built-in Auto-Retry

**Status**: Accepted (Rejection)

### Context
Brighter has built-in retry policies. Some mediator users want automatic retry on transient failures.

### Decision
**REJECTED from core**. Retry belongs to `EricksonLopez.Mediator.Polly` extension package.

### Why
- Retry policies require configuration (what exceptions? how many times? what backoff?)
- "Auto-retry" without configuration is dangerous (retrying non-idempotent commands)
- Polly is the standard .NET library for resilience. Duplicating its functionality is wasteful.
- A `RetryBehavior<TRequest, TResponse>` can be written using Polly and `IPipelineBehavior` in 30 lines

### Competitive Impact
Neutral. Users who need retry will use the behavior pattern. This is not a feature gap.

---

