# Frequently Asked Questions (FAQ)

Answers to common questions regarding the architecture, operation, and security of `EricksonLopez.Auditing`.

---

## Conceptual & Architecture

### What is the difference between Auditing, Logging, and Distributed Tracing?

| Dimension | Diagnostic Logging | Distributed Tracing | Forensic Auditing |
| :--- | :--- | :--- | :--- |
| **Primary Goal** | System health & troubleshooting | Performance & latency bottlenecks | Non-repudiable proof of business actions |
| **Producer** | Automated subsystem | Instrumentation SDK | Authenticated human or service actor |
| **Integrity** | Best effort, mutable | Best effort, sampled | Cryptographic HMAC-SHA256, append-only |
| **Scope** | Technical exceptions & debug info | HTTP/RPC spans & timings | Domain, financial & security events |
| **Retention** | Days to weeks | Days to weeks | Years to indefinite |
| **Regulations** | N/A | N/A | SOC2, PCI-DSS, GDPR, HIPAA |

---

### Why does `IAuditStore` not expose `UpdateAsync()` or `DeleteAsync()` methods?

Audit records represent immutable legal evidence. Enabling updates or deletions within the SPI would violate compliance non-repudiation invariants. If an operation was performed in error, the proper forensic pattern is to emit a new compensating audit record with `AuditAction.Cancel` or `AuditAction.Restore`, maintaining an unbroken historical record.

---

### When should I use `AuditContext.SystemTenantId`?

Use `AuditContext.SystemTenantId` (`"system"`) when an event belongs to the platform or infrastructure rather than an isolated tenant. Examples include:
* Automated cleanup of expired user sessions.
* Cryptographic key rotation jobs.
* Global system configuration changes.
* Background worker maintenance.

```csharp
var context = new AuditContext(
    TenantId: AuditContext.SystemTenantId,
    Source: "MaintenanceService");
```

---

### What is UUIDv7 and why use `AuditId.NewId()` instead of `Guid.NewGuid()`?

`Guid.NewGuid()` generates random UUIDv4 identifiers. When inserted into relational databases, random UUIDs insert into arbitrary index positions, causing severe B-Tree page splits and table fragmentation. `AuditId.NewId()` generates RFC 9562 UUIDv7 identifiers, which encode Unix millisecond timestamps in high-order bits. This ensures sequential inserts at the end of the index (matching auto-increment performance) while retaining distributed uniqueness.

---

### Can I create custom `AuditAction` codes?

Yes. `AuditAction` is a `readonly record struct` with a string `Code`. You can define domain-specific actions:

```csharp
public static class FinanceAuditActions
{
    public static readonly AuditAction ProcessWireTransfer = new("ProcessWireTransfer");
    public static readonly AuditAction ReconcileLedger = new("ReconcileLedger");
}
```

---

## Performance & Throughput

### What throughput can the library achieve?

The auditing library introduces minimal overhead (sub-microsecond CPU time). Throughput is governed by database I/O:
* Using `AppendBatchAsync(1000)` on PostgreSQL over a local connection: ~18,000 to 25,000 records/second single-threaded.
* With pooled connections and parallel batch workers: 100,000+ records/second.

---

### Is `AppendBatchAsync()` atomic?

Yes. In relational adapters (PostgreSQL, SQL Server, MySQL, Oracle, SQLite), `AppendBatchAsync` issues a single multi-row `INSERT` statement within an implicit transaction. In MongoDB, it executes `InsertManyAsync`.

---

### Which `AuditFailureBehavior` should I choose?

| Mode | Behavior on Store Error | Recommended Usage |
| :--- | :--- | :--- |
| `FailClosed` | Throws exception, aborts operation | Critical operations (financial transfers, privilege grants, GDPR compliance). |
| `FailOpen` | Logs error, allows caller to proceed | High-availability non-critical actions where downtime is unacceptable. |
| `Deferred` | Enqueues to local channel for retry | Systems with intermittent database connectivity or outbox workers. |

---

## Security & Forensics

### How do I detect if someone modified a row directly in the database?

With `EnableIntegrityChain()` enabled, run `IAuditIntegrityVerifier.VerifyChainAsync()`:

```csharp
var result = await verifier.VerifyChainAsync("tenant-acme",
    from: DateTimeOffset.UtcNow.AddDays(-30),
    until: DateTimeOffset.UtcNow);

if (!result.IsValid)
{
    // result.FirstFailedRecordId identifies the modified or deleted record
    // result.FailureReason describes the detected tampering pattern
}
```

---

### Is storing `TenantId` in plain text secure?

Yes. `TenantId` is a routing and scoping identifier, not a credential. Multi-tenant isolation is enforced at the database level using PostgreSQL `FORCE ROW LEVEL SECURITY`, SQL Server `SESSION_CONTEXT`, Oracle VPD, or indexed tenant query filters.

---

## Testing

### How do I unit-test services that emit audit records?

Use `InMemoryAuditStore` from the `EricksonLopez.Auditing.Testing` package:

```csharp
var store = new InMemoryAuditStore();
var service = new OrderService(store);

await service.ApproveOrderAsync("ord-1", "tenant-test", CancellationToken.None);

var records = store.ForTenant("tenant-test");
Assert.Single(records);
Assert.Equal(AuditAction.Approve.Code, records[0].Action.Code);
```
