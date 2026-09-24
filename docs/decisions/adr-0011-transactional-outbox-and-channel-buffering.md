<!-- Copyright © Erickson Lopez. MIT License. -->
# ADR-0011: Transactional Outbox Pattern and Asynchronous Channel Buffering

## Status
Accepted

## Date
2026-09-14

## Context
High-throughput applications face significant latency penalties and dual-write inconsistency risks when synchronous database inserts are executed on the critical HTTP request path for every audit record:
1. **Request Latency**: Synchronously waiting for remote database roundtrips on every audit write degrades application throughput and increases tail latency.
2. **Dual-Write Failures**: In transactional microservices, if the core domain transaction succeeds but the synchronous audit insert fails due to transient database or network unavailability, the system enters an inconsistent state.
3. **Thread Pool Contention**: Unbounded thread creation or synchronous blocking during high-volume bursts exhausts thread pool workers.

## Decision
We introduce two decoupled, complementary persistence decorators:
1. **`BufferedAuditStoreDecorator` (`EricksonLopez.Auditing`)**:
   - Wraps any underlying `IAuditStore` using `System.Threading.Channels` (`Channel<AuditRecord>`).
   - Writes to a bounded in-memory buffer with configurable capacity (`BatchChannelCapacity`), batch sizing (`BatchSize`), and timeout flushing (`BatchFlushInterval`).
   - A dedicated asynchronous background worker drains batches via `IAuditStore.AppendBatchAsync()` without blocking caller request threads.
2. **`OutboxAuditStore` (`EricksonLopez.Auditing.Outbox`)**:
   - Implements the Transactional Outbox pattern via `IOutboxMessageService`.
   - Stages audit records in the local transactional context of the business transaction, ensuring atomic commit with domain state changes.
   - An asynchronous outbox dispatcher periodically queries and forwards pending records to the final audit store or messaging broker.

## Consequences

### Positive
* Zero blocking latency on caller threads: synchronous web requests return immediately after enqueuing.
* High batching efficiency: reduces total database roundtrips by orders of magnitude via micro-batching.
* Guaranteed atomicity: Transactional Outbox eliminates the dual-write problem across distributed operations.
* Graceful backpressure: Bounded channel capacities prevent out-of-memory errors under massive burst traffic.

### Negative / Trade-offs
* Eventual consistency: Records are not instantly visible in query stores until flushed by the background worker.
* In-flight buffer risk: In ungraceful process crashes without outbox durability, records pending in non-persisted channel buffers could be lost unless configured with persistent staging.
