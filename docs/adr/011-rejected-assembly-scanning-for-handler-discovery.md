# ADR-011: REJECTED — Assembly Scanning for Handler Discovery

**Status**: Accepted (Rejection)

### Context
`services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Startup).Assembly))` is a popular pattern that scans assemblies at startup.

### Decision
**REJECTED**. No assembly scanning in EricksonLopez.Mediator.

### Why
- Assembly scanning is O(n) in types count, adding startup latency
- Assembly scanning is incompatible with AOT trimming
- The source generator provides compile-time handler discovery
- Runtime discovery provides less value than compile-time discovery (no IDE errors)

### Consequences
- Users cannot discover handlers from external assemblies automatically (see ADR-012 for multi-assembly)
- This is the correct trade-off: explicit over magic

---

