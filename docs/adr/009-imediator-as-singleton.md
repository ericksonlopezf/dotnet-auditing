# ADR-009: IMediator as Singleton

**Status**: Accepted

### Decision
`GeneratedMediator` (implementing `IMediator`) is registered as Singleton.

### Why
- `GeneratedMediator` holds only an `IServiceProvider` reference (thread-safe)
- No mutable state in the mediator itself
- Singleton reduces DI resolution overhead
- Handlers are resolved per-call from IServiceProvider (correct lifetime management)

### Consequences
+ Zero DI resolution overhead for IMediator
+ Thread-safe by construction
- Developer cannot have per-request state in the mediator (correct — use handlers instead)

---

# PARTE B: ADRs DE FEATURES RECHAZADAS

---

