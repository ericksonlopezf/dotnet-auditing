# ADR-019: REJECTED — Outbox, Inbox, Saga, Scheduling

**Status**: Accepted (Rejection)

### Context
Wolverine and MassTransit include outbox, inbox, saga, and scheduling in their frameworks.

### Decision
**REJECTED**. These are distributed systems concerns, not in-process mediator concerns.

### Why
- The outbox pattern requires persistence. The mediator has no persistence layer.
- Sagas require state machines and potentially distributed coordination.
- Scheduling requires a scheduler (Quartz, Hangfire, Azure Scheduler).
- Including these would transform EricksonLopez.Mediator into a mini-framework, adding enormous complexity and maintenance burden for features that most users don't need.

### Ecosystem
- Outbox: `EricksonLopez.Outbox` (separate package)
- Sagas: Not in ecosystem scope (recommend MassTransit or Wolverine for saga needs)
- Scheduling: Not in ecosystem scope (recommend Hangfire, Quartz, or .NET Worker Service)

### Competitive Impact
Intentional scope limitation. Being excellent at one thing is better than being mediocre at many things.

---

