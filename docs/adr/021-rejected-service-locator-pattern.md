# ADR-021: REJECTED — Service Locator Pattern

**Status**: Accepted (Rejection)

### Context
Some mediator implementations use a service locator (calling `IServiceProvider.GetService(typeof(T))` with dynamic types) to resolve handlers.

### Decision
**REJECTED**. The generated dispatcher uses `GetRequiredService<TConcreteType>()` (concrete, known at compile time) only. Dynamic `GetService(Type)` is forbidden.

### Why
- `GetService(typeof(T))` with a dynamic type object is AOT-unsafe
- Service locator is an anti-pattern: it hides dependencies
- The generated switch provides explicit, traceable dispatch paths
- Every handler type is known at compile time — dynamic resolution adds no value

---

