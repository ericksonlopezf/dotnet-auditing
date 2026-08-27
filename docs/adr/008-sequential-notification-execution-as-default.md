# ADR-008: Sequential Notification Execution as Default

**Status**: Accepted

### Context
Notifications are broadcast to multiple handlers. The execution strategy (sequential vs parallel) has significant implications for correctness, exception handling, and transaction boundaries.

### Decision
Default execution strategy is sequential. Handlers execute one after another. First failure stops the chain.

### Why
- Sequential is deterministic and predictable
- Parallel execution of handlers sharing a scoped DbContext would cause concurrency issues
- "First failure stops" is safer than "continue on error" because subsequent handlers may depend on state from previous ones
- Parallel execution can always be added via a behavior or as opt-in attribute

### Consequences
+ Safe for handlers sharing scoped services
+ Predictable execution order
+ Simple implementation (generated sequential awaits)
- Slightly slower for handlers that are truly independent (post-MVP optimization)

### Reconsideration Criteria
If parallel notification demand is high, add `[assembly: NotificationStrategy(Strategy.Parallel)]` in v1.x.

---

