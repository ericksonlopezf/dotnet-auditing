# ADR-010: REJECTED — Runtime Reflection for Dispatch

**Status**: Accepted (Rejection)

### Context
Runtime reflection (`MethodInfo.Invoke`, `Activator.CreateInstance`, `GetService(typeof(T))` with dynamic types) is used by MediatR, Brighter, and MassTransit Mediator for handler dispatch.

### Problem
This is fundamentally incompatible with Native AOT and IL trimming.

### Options Considered
1. Use reflection for discovery, cache for performance — still requires `[RequiresDynamicCode]`
2. Use expression compilation (`Expression.Compile`) — still requires dynamic code
3. **Source generator — zero reflection** — our choice

### Decision
**REJECTED**. Runtime reflection is FORBIDDEN in EricksonLopez.Mediator.

### Why
- `[RequiresDynamicCode]` on any public API violates our Native AOT primary feature
- Reflection breaks trimming: the trimmer removes unreferenced types, and reflection-based discovery defeats this
- The source generator provides the same functionality without the runtime cost
- Any feature requiring reflection must be rejected or redesigned

### Competitive Impact
This is our primary differentiator. MediatR cannot support Native AOT natively because it is built on reflection. We are built on the assumption that reflection is never needed.

### Reconsideration Criteria
None. This is a foundational design principle.

---

