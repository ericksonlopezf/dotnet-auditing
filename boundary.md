# Architectural Boundary Specification: EricksonLopez.Auditing

## 1. Purpose

`EricksonLopez.Auditing.Abstractions` defines the canonical immutable audit record contracts, audit builder interfaces, and storage SPI for compliance tracking and forensic audit trails in enterprise .NET applications.

## 2. Owns

* Storage SPI: `IAuditStore`.
* Canonical Records & Types: `AuditRecord`, `AuditActor`, `AuditActorType`, `AuditAction`, `AuditResource`, `AuditContext`, `AuditChange`, `AuditOutcome`, `AuditQuery`, `AuditQueryResult`, `AuditIntegrityVerificationResult`.
* Provider Interfaces: `IAuditActorProvider`, `IAuditContextProvider`, `IAuditIntegrityProvider`, `IAuditIntegrityVerifier`, `SystemAuditActorProvider`.
* Builder Contract: `IAuditBuilder`.
* Pure Cryptographic Service: `HmacAuditIntegrityService`.

## 3. Does Not Own

* Ambient audit scope orchestration (`EricksonLopez.Auditing`).
* Runtime sensitivity filtering & redaction pipeline (`EricksonLopez.Auditing`).
* Concrete database audit storage engines (`PostgreSql`, `SqlServer`, `MySql`, `Oracle`, `Sqlite`, `MongoDb`, `EntityFrameworkCore`, `Dapper`).
* Test doubles and in-memory test stores (`EricksonLopez.Auditing.Testing`).
* Observability instrumentation (`EricksonLopez.Auditing.OpenTelemetry`).

## 4. Allowed Dependencies

* **.NET BCL only** (`System.*`).
* `Microsoft.Extensions.DependencyInjection.Abstractions`.
* **Zero** third-party runtime or database dependencies.

## 5. Forbidden Dependencies

* Concrete database driver SDKs (`Npgsql`, `Microsoft.Data.SqlClient`, `MongoDB.Driver`, `MySqlConnector`, `Oracle.ManagedDataAccess.Core`, `Microsoft.Data.Sqlite`).
* ORMs (`Microsoft.EntityFrameworkCore`, `Dapper`).
* Core engine (`EricksonLopez.Auditing`).

## 6. Dependency Graph & Layers

```text
Layer 1 (Foundation):  EricksonLopez.Auditing.Abstractions
Layer 2 (Core Engine): EricksonLopez.Auditing -> Abstractions
Layer 3 (Testing):     EricksonLopez.Auditing.Testing -> Abstractions, Core
Layer 4 (Telemetry):   EricksonLopez.Auditing.OpenTelemetry -> Abstractions
Layer 5 (Adapters):    EricksonLopez.Auditing.PostgreSql / SqlServer / MySql / Oracle / Sqlite / Dapper / MongoDb / EntityFrameworkCore -> Abstractions
```

## 7. Public API Invariants

* **Immutability:** Audit records are non-repudiable legal evidence; modifying persisted entries via `IAuditStore` is prohibited by contract (no `Update` or `Delete` APIs).
* **Deterministic IDs:** IDs must be generated via monotonic UUIDv7 (`AuditId.NewId()`).
* **Tenant Isolation:** Multi-tenancy is strictly enforced across all database queries and batch operations.

## 8. AOT & Trimming Expectations

* `IsAotCompatible = true`.
* `EnableTrimAnalyzer = true`.
* Zero dynamic reflection in public serialization pathways.
