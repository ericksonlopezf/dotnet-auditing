# Architectural Decision Record: REJECT-002
## Rejection of Stateful Workflow and Saga Management inside Mediator

### Status
**REJECTED (Permanent Directorial Invariant)**

### Context
There have been inquiries regarding whether `EricksonLopez.Mediator` should incorporate state machines, step retry timers, or multi-step saga orchestration state directly within its dispatch pipeline.

### Decision
Permanently rejected. `EricksonLopez.Mediator` is strictly an in-memory, zero-allocation, strongly typed command/query/notification dispatcher. All stateful process management, saga orchestration, compensation, and snapshot persistence belongs exclusively to `EricksonLopez.Processes`.

### Consequences
- Single Responsibility: Mediator handles dispatching only.
- Process state persistence and compensation logic live in `EricksonLopez.Processes`.
- Mediator performance profile remains optimal with zero runtime state overhead.
