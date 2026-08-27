# Performance & Optimization Guide: EricksonLopez.Auditing

Techniques and best practices to maximize throughput, minimize database I/O, and maintain high scalability in production.

---

## 1. Monotonic Identifiers: `AuditId.NewId()` vs `Guid.NewGuid()`

| Metric | `Guid.NewGuid()` (UUIDv4) | `AuditId.NewId()` (UUIDv7 - RFC 9562) |
| :--- | :--- | :--- |
| **Ordering** | Completely random | Monotonically time-ordered |
| **Index Insert Location** | Arbitrary index page | Always at the tail end of index |
| **B-Tree Page Splits** | **High** (causes fragmentation and heavy disk writes) | **Zero** (equivalent to auto-increment append) |
| **Distributed Uniqueness** | Global | Global (Timestamp + Random bits) |

**Rule:** Always use `AuditId.NewId()` for audit record creation.

---

## 2. Keyset Cursor Pagination ($O(1)$) vs OFFSET ($O(N)$)

```sql
-- ❌ Inefficient OFFSET — Scans and discards N rows:
SELECT * FROM audit.records WHERE tenant_id = 'acme' ORDER BY occurred_at DESC, id DESC OFFSET 50000 LIMIT 20;

-- ✅ Keyset Pagination — Direct index seek:
SELECT * FROM audit.records 
WHERE tenant_id = 'acme' AND (occurred_at, id) < (@LastOccurredAt, @LastId)
ORDER BY occurred_at DESC, id DESC LIMIT 20;
```

**Rule:** Use `AuditQuery.AfterRecordId` to seek directly to the next cursor without table scans.

---

## 3. High-Throughput Batching: `AppendBatchAsync()`

```csharp
// ❌ Inefficient: N network round-trips to the database
foreach (var record in records)
{
    await store.AppendAsync(record);
}

// ✅ Efficient: Single multi-row INSERT / batched write
await store.AppendBatchAsync(records);
```

### Indicative Benchmark Comparison (PostgreSQL, net9.0, local connection):

| Operation | 100 Records | 1,000 Records | 10,000 Records |
| :--- | :--- | :--- | :--- |
| `AppendAsync` $\times N$ | ~450 ms | ~4.5 s | ~45 s |
| `AppendBatchAsync` (1 batch) | **~8 ms** | **~45 ms** | **~420 ms** |

`AppendBatchAsync` provides **10x to 50x higher throughput** for high-volume ingest.

---

## 4. Recommended Composite Index Strategy

To ensure queries hit index seeks with zero heap fetches:

```sql
-- PostgreSQL / SQL Server / MySQL
CREATE INDEX idx_audit_records_tenant_time_id
    ON audit.records (tenant_id, occurred_at DESC, id DESC);

-- Covering Index with frequently filtered columns:
CREATE INDEX idx_audit_records_covering
    ON audit.records (tenant_id, occurred_at DESC, id DESC)
    INCLUDE (actor_id, action_code, resource_type, outcome);
```

---

## 5. Time-Range Partitioning (PostgreSQL)

For tables managing hundreds of millions of audit entries, monthly partitioning optimizes index size and simplifies data lifecycle retention:

```sql
CREATE TABLE audit.records (
    id UUID NOT NULL,
    occurred_at TIMESTAMPTZ NOT NULL,
    tenant_id TEXT NOT NULL,
    -- ...
    PRIMARY KEY (id, occurred_at)
) PARTITION BY RANGE (occurred_at);

-- Monthly partition:
CREATE TABLE audit.records_2026_08 PARTITION OF audit.records
    FOR VALUES FROM ('2026-08-01') TO ('2026-09-01');
```

---

## 6. Connection Pool Optimization

Ensure the database connection string configures an appropriate pool size for high-concurrency workloads:

```csharp
builder.Services.AddAuditing()
    .UsePostgreSql(options =>
    {
        options.ConnectionFactory = () =>
            new NpgsqlConnection("Host=localhost;Database=audit;Username=audit_app;Password=...;MaxPoolSize=100;MinPoolSize=10");
    });
```

---

## 7. Asynchronous Pipeline Batching

For streaming event workloads, configure in-memory buffer capacities:

```csharp
builder.Services.AddAuditing(cfg =>
{
    cfg.BatchChannelCapacity = 10_000;
    cfg.BatchSize = 500;
    cfg.BatchFlushInterval = TimeSpan.FromSeconds(1);
});
```

---

## 8. Running Benchmarks

Execute the included BenchmarkDotNet suite:

```bash
# Run all benchmarks
dotnet run --project benchmarks/EricksonLopez.Auditing.Benchmarks/EricksonLopez.Auditing.Benchmarks.csproj -c Release

# Run specific benchmark category (e.g. UUIDv7 vs Guid.NewGuid)
dotnet run --project benchmarks/EricksonLopez.Auditing.Benchmarks/EricksonLopez.Auditing.Benchmarks.csproj -c Release -- --filter "*AuditId*"
```
