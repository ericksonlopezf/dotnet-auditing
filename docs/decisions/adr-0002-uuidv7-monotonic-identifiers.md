# ADR-0002: Monotonic Time-Ordered Identifiers via RFC 9562 UUIDv7

## Context

Using random UUIDs (UUIDv4 via `Guid.NewGuid()`) as primary keys in relational databases causes severe B-Tree page splits and index fragmentation during high-throughput append-only operations. Auto-incrementing integer IDs mitigate this but suffer from distributed collision risks, instance coordination bottlenecks, and predictable ID exposure.

## Decision

All audit record IDs are generated via `AuditId.NewId()`, which implements RFC 9562 UUIDv7. UUIDv7 embeds a 48-bit Unix millisecond timestamp in the most significant bits followed by cryptographically random bits.

## Consequences

### Positive
* Inserts append monotonically at the tail end of B-Tree indexes, matching auto-increment performance while maintaining distributed uniqueness.
* Eliminates index fragmentation and page splits in PostgreSQL, SQL Server, MySQL, Oracle, and SQLite.
* Enables timestamp extraction and natural temporal clustering.

### Negative / Trade-offs
* Requires careful bit manipulation to guarantee monotonic ordering under high-frequency sub-millisecond generation.
* On .NET 8 (where `Guid.CreateVersion7()` is unavailable), the manual fallback implementation uses random bits in `rand_b`, which provides probabilistic but **not guaranteed** monotonic ordering within the same millisecond. The .NET 9+ path delegates to `Guid.CreateVersion7()`, which handles sub-millisecond monotonicity natively.
