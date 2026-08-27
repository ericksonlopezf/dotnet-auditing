# Feature Gaps & Systematic Rejections (ADR Discards)

## 1. Intentional Architectural Boundaries

The following capabilities have been formally reviewed via Architectural Decision Records (ADRs) and rejected from the core mediator kernel:

| Discarded Feature | ADR Reference | Rationale | Recommended Architectural Pattern |
|---|---|---|---|
| **Runtime DI Scanning** | ADR-011 | Startup latency penalty and Native AOT trimming breakage. | Static compile-time source generation (`AddMediator()`). |
| **Delegate Pipeline Closures** | ADR-013 | Induces heap allocations and GC Gen 0 pressure. | Zero-allocation struct `INext<TResponse>` continuations. |
| **Built-in Auto-Retry in Core** | ADR-014 | Blurs concerns and duplicates battle-tested resilience engines. | Use `EricksonLopez.Mediator.Polly` with Polly v8. |
| **Built-in Outbox / Inbox / Sagas** | ADR-019 | Transports and persistence mechanics belong in infrastructure messaging busses (e.g. MassTransit, Wolverine, RabbitMQ). | In-process mediator triggers domain events; infrastructure handles distributed broker publishing. |
| **State Machine in Mediator** | Reject-002 | Stateful workflows belong in dedicated saga or orchestrator engines. | Pure stateless in-process command/query mediation. |
