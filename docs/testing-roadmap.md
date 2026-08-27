# Testing Roadmap & Functional Unit Coverage — EricksonLopez.Auditing

## 1. Overview & Strategy

The testing strategy for `EricksonLopez.Auditing` is built on three pillars:
1. **Modular Unit Tests (1:1 per package)**: Fast, memory-only, deterministic execution covering core domain invariants, redaction logic, hash chaining, and provider query formatting.
2. **Integration Tests (Testcontainers)**: Container-backed end-to-end persistence validation against true PostgreSQL, SQL Server, MySQL, Oracle, SQLite, and MongoDB engines.
3. **Mutation Testing (Stryker.NET)**: Matrix-driven mutation verification enforcing a `break: 95%` quality gate with 100% killed mutants target across all packages.

---

## 2. Functional Unit Breakdown

| Unit ID | Scope / Package | Functional Invariant Under Test | Test Suite Project |
|---|---|---|---|
| **U01** | `Abstractions` | `AuditId` UUIDv7 monotonic ordering, timestamp extraction, equality | `EricksonLopez.Auditing.Abstractions.Tests` |
| **U02** | `Abstractions` | `AuditRecord` immutability, payload encapsulation, metadata contracts | `EricksonLopez.Auditing.Abstractions.Tests` |
| **U03** | `Abstractions` | `IAuditStore` and `IAuditIntegrityValidator` SPI interfaces | `EricksonLopez.Auditing.Abstractions.Tests` |
| **U04** | `Core` | `AuditScope` lifecycle, ambient context, automatic flushing | `EricksonLopez.Auditing.Tests` |
| **U05** | `Core` | HMAC-SHA256 integrity hash chaining & tamper detection | `EricksonLopez.Auditing.Tests` |
| **U06** | `Core` | Sensitive data redaction pipeline & PII masking rules | `EricksonLopez.Auditing.Tests` |
| **U07** | `Core` | Dependency injection registration & options validation | `EricksonLopez.Auditing.Tests` |
| **U08** | `Dapper` | Parameter binding, dynamic mapping, and raw SQL generation | `EricksonLopez.Auditing.Dapper.Tests` |
| **U09** | `EntityFrameworkCore` | Change tracker interception, entity diff extraction, audit dispatch | `EricksonLopez.Auditing.EntityFrameworkCore.Tests` |
| **U10** | `PostgreSql` | JSONB serialization, table partitioning, RLS compatibility | `EricksonLopez.Auditing.PostgreSql.Tests` |
| **U11** | `SqlServer` | Temporal table compatibility, structured parameter emission | `EricksonLopez.Auditing.SqlServer.Tests` |
| **U12** | `Sqlite` | Embedded WAL mode transactions, synchronous append-only writes | `EricksonLopez.Auditing.Sqlite.Tests` |
| **U13** | `MySql` | UTF8MB4 charset, JSON document storage, monotonic indexing | `EricksonLopez.Auditing.MySql.Tests` |
| **U14** | `Oracle` | Sequence handling, RAW / BLOB storage, RAWTOHEX conversions | `EricksonLopez.Auditing.Oracle.Tests` |
| **U15** | `MongoDb` | BSON document mapping, index creation, capped collection support | `EricksonLopez.Auditing.MongoDb.Tests` |
| **U16** | `OpenTelemetry` | Activity creation, span attributes, audit event tracing metrics | `EricksonLopez.Auditing.OpenTelemetry.Tests` |
| **U17** | `Testing` | In-memory test store, assertion helpers, test harness spies | `EricksonLopez.Auditing.Testing.Tests` |
| **I01** | `Integration` | Multi-engine real container persistence & roundtrip verification | `EricksonLopez.Auditing.IntegrationTests` |
| **S01** | `NativeAOT` | Zero reflection Native AOT compilation & trimming smoke test | `EricksonLopez.Auditing.AotSmokeTest` |

---

## 3. Quality Gate Thresholds

```text
Unit Test Line Coverage: ≥ 99.0%
Unit Test Branch Coverage: ≥ 98.0%
Mutation Testing Score: ≥ 95.0% (Enforced break threshold; 100% achieved)
Native AOT Smoke Test: Zero warnings, PublishAot=true runtime exit code 0
```
