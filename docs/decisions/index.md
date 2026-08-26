# Architecture Decision Records (ADRs)

This directory documents the significant architectural decisions made in the `EricksonLopez.Auditing` ecosystem following the standard ADR format.

---

## Decision Log

| ADR | Title | Status | Date |
| :--- | :--- | :---: | :---: |
| [ADR-0001](adr-0001-append-only-immutable-audit-store.md) | Append-Only Immutable Storage Contract (`IAuditStore`) | **Accepted** | 2026-08-26 |
| [ADR-0002](adr-0002-uuidv7-monotonic-identifiers.md) | Monotonic Time-Ordered Identifiers via RFC 9562 UUIDv7 | **Accepted** | 2026-08-26 |
| [ADR-0003](adr-0003-database-level-multi-tenant-isolation.md) | Database-Level Multi-Tenant Isolation (RLS / Session Context / VPD) | **Accepted** | 2026-08-26 |
| [ADR-0004](adr-0004-hmac-sha256-cryptographic-integrity-chain.md) | Cryptographic Tamper-Evidence via HMAC-SHA256 Chaining | **Accepted** | 2026-08-26 |
| [ADR-0005](adr-0005-native-aot-source-generated-json.md) | Zero-Reflection Native AOT & Trimming Serialization | **Accepted** | 2026-08-26 |
| [ADR-0006](adr-0006-sensitive-data-redaction-pipeline.md) | Global Sensitive Data Protection & Redaction Pipeline | **Accepted** | 2026-08-26 |
| [ADR-0007](adr-0007-keyset-cursor-pagination.md) | Keyset Pagination for O(1) Large Scale Queries | **Accepted** | 2026-08-26 |
| [ADR-0008](adr-0008-decoupled-storage-provider-spi.md) | Decoupled Storage Provider Architecture (SPI Pattern) | **Accepted** | 2026-08-26 |

