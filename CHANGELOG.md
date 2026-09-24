<!-- Copyright © Erickson Lopez. MIT License. -->
# Changelog

All notable changes to the `EricksonLopez.Auditing` ecosystem will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [2.0.0] — 2026-09-23

### 💥 Breaking Changes

- **Domain & Strongly-Typed Multi-Tenancy (`AuditContext.TenantId` & `AuditQuery.TenantId`)**:
  - **What changed:** Property and constructor parameter `TenantId` in `AuditContext` and `AuditQuery` changed from `string` to `TenantId` (readonly record struct).
  - **Previous state (v1.0.0):** `public sealed record AuditContext(string TenantId, string Source, ...)` and `public required string TenantId { get; init; }`.
  - **Current state (v2.0.0):** `public sealed record AuditContext(TenantId TenantId, string Source, ...)` and `public required TenantId TenantId { get; init; }`.
  - **Affected:** Consumers instantiating `AuditContext` or `AuditQuery` via reflection, object mappers, or positional record deconstruction expecting `string`.
  - **Impact:** Compilation error `CS1503` / `CS0029` on type assignments and positional deconstructions.
  - **Migration:** Update calls to instantiate `new TenantId("id")` or leverage the implicit conversion operator from `string`.

- **System Tenant Constant in Context (`AuditContext.SystemTenantId`)**:
  - **What changed:** `SystemTenantId` changed from a compile-time constant literal `const string` to an immutable static field `static readonly TenantId`.
  - **Previous state (v1.0.0):** `public const string SystemTenantId = "system";`.
  - **Current state (v2.0.0):** `public static readonly TenantId SystemTenantId = new TenantId("system");`.
  - **Affected:** Code with constant `switch/case` statements or compile-time attributes, as well as binaries compiled against v1.0.0 without recompilation.
  - **Impact:** Causes runtime `MissingFieldException` for assemblies compiled against v1.0.0 and compilation error `CS0150` in `case AuditContext.SystemTenantId:` statements.
  - **Migration:** Replace constant `case` statements with conditional guards `if (context.TenantId == AuditContext.SystemTenantId)` or pattern matching with `when`, or access `AuditContext.SystemTenantId.Value`.

- **Keyset Pagination: Removal of `AuditQuery.AfterRecordId`**:
  - **What changed:** Removed property `AfterRecordId` (`Guid?`) in favor of opaque continuation cursor `ContinuationToken` (`string?`).
  - **Previous state (v1.0.0):** `public Guid? AfterRecordId { get; init; }`.
  - **Current state (v2.0.0):** Property removed. New property: `public string? ContinuationToken { get; init; }`.
  - **Affected:** Any query consumer or presentation layer implementing pagination based on `Guid` record identifiers.
  - **Impact:** Compilation error `CS0117: 'AuditQuery' does not contain a definition for 'AfterRecordId'`.
  - **Migration:** Supply the opaque base64 cursor token received from `AuditQueryResult.NextPageToken` into `AuditQuery.ContinuationToken`.

- **Keyset Pagination: Removal of `AuditQueryResult.NextCursorId`**:
  - **What changed:** Removed property and record parameter `NextCursorId` (`Guid?`), replaced by `NextPageToken` (`string?`).
  - **Previous state (v1.0.0):** `public sealed record AuditQueryResult(IReadOnlyList<AuditRecord> Records, Guid? NextCursorId, bool HasMore)`.
  - **Current state (v2.0.0):** `public sealed record AuditQueryResult(IReadOnlyList<AuditRecord> Records, string? NextPageToken, bool HasMore)`.
  - **Affected:** Consumers reading `result.NextCursorId` or deconstructing the positional record tuple expecting `Guid?`.
  - **Impact:** Compilation error `CS1061: 'AuditQueryResult' does not contain a definition for 'NextCursorId'` and tuple assignment mismatches.
  - **Migration:** Access `result.NextPageToken` (`string?`) to retrieve the continuation token for the subsequent page.

- **Query Contract: Strongly-Typed TenantId in `AuditQuery.TenantId`**:
  - **What changed:** The required `TenantId` property on `AuditQuery` now requires the `TenantId` struct value object.
  - **Previous state (v1.0.0):** `public required string TenantId { get; init; }`.
  - **Current state (v2.0.0):** `public required TenantId TenantId { get; init; }`.
  - **Affected:** Query invocations initializing `AuditQuery` explicitly or via dynamic binding.
  - **Impact:** Type incompatibility in object initializers and reflection APIs.
  - **Migration:** Initialize with `TenantId = new TenantId("tenant-key")` or directly assign an existing `TenantId` instance.

- **Integrity SPI Interface Signature (`IAuditIntegrityProvider`)**:
  - **What changed:** The `GetCurrentKey` method on public interface `IAuditIntegrityProvider` changed its parameter from `string` to `TenantId`.
  - **Previous state (v1.0.0):** `ReadOnlyMemory<byte> GetCurrentKey(string tenantId);`.
  - **Current state (v2.0.0):** `ReadOnlyMemory<byte> GetCurrentKey(TenantId tenantId);`.
  - **Affected:** All external or custom classes implementing `IAuditIntegrityProvider`.
  - **Impact:** Compilation error `CS0535: does not implement interface member 'IAuditIntegrityProvider.GetCurrentKey(TenantId)'`.
  - **Migration:** Update method signature in custom implementations to `public ReadOnlyMemory<byte> GetCurrentKey(TenantId tenantId)`.

- **Removal of Synchronous `AuditSensitivityPipeline.Apply` Method**:
  - **What changed:** Removed public synchronous method `Apply(IReadOnlyList<AuditChange>? changes)`.
  - **Previous state (v1.0.0):** `public IReadOnlyList<AuditChange>? Apply(IReadOnlyList<AuditChange>? changes)`.
  - **Current state (v2.0.0):** Method removed. Replaced by `public ValueTask<IReadOnlyList<AuditChange>?> ApplyAsync(IReadOnlyList<AuditChange>? changes, string tenantId, CancellationToken cancellationToken = default)` and `public ValueTask<AuditRecord> SanitizeAsync(AuditRecord record, CancellationToken cancellationToken = default)`.
  - **Affected:** Consumers executing in-memory change sanitization synchronously.
  - **Impact:** Compilation error `CS1061: 'AuditSensitivityPipeline' does not contain a definition for 'Apply'`.
  - **Migration:** Replace invocations with `await pipeline.ApplyAsync(changes, tenantId)` or `await pipeline.SanitizeAsync(record)`.

- **Cryptographic Restructuring of Canonical Hash (`HmacAuditIntegrityService`)**:
  - **What changed:** HMAC-SHA256 calculation redesigned from a 9-field pipe-delimited format (`|`) to a length-prefixed binary format (`length~value|`) spanning 20+ fields, incorporating alphabetically sorted `Changes`, `Source`, `DisplayName`, `AggregateType`, `AggregateId`, `ErrorCode`, `CorrelationId`, `CausationId`, `RequestId`, `IpAddress`, and `UserAgent`.
  - **Previous state (v1.0.0):** Basic hash over 9 header metadata fields excluding change deltas and extended context metadata.
  - **Current state (v2.0.0):** Comprehensive forensic hash guaranteeing end-to-end non-repudiation over the entire evidence payload.
  - **Affected:** All organizations verifying records generated under v1.0.0 using `IAuditIntegrityVerifier` or `HmacAuditIntegrityService.VerifyRecord`.
  - **Impact:** **Cryptographic Incompatibility:** Historical records generated in v1.0.0 will fail verification in v2.0.0, falsely flagging records as corrupted or tampered (`IsValid = false`).
  - **Migration:** Implement a dual-verification strategy validating against the canonical v1 format for records persisted prior to v2.0.0 migration.

- **Automatic Truncation of Oversized Text Values (`MaxStringLength`)**:
  - **What changed:** Introduced payload length bounding in `AuditSensitivityPipeline` to prevent large-object-heap (LOH) memory exhaustion attacks and database column overflow exceptions.
  - **Previous state (v1.0.0):** `OldValue` and `NewValue` strings were stored unconstrained regardless of payload size.
  - **Current state (v2.0.0):** Values exceeding `AuditConfiguration.MaxStringLength` (default: 4000 characters) are truncated to 4000 characters with suffix `"[TRUNCATED]"`.
  - **Affected:** Systems auditing large text payloads (documents, legal contracts, large JSON documents).
  - **Impact:** Silent truncation of trailing content in audited change deltas.
  - **Migration:** Configure `AuditConfiguration.MaxStringLength` (up to 8192) in DI registration, or store large payloads in blob storage auditing only their content hash or URI.

- **Strict Validation Invariants on Domain Models**:
  - **What changed:** Domain types `AuditActor`, `AuditResource`, and `AuditContext` now throw `ArgumentException` if initialized with null, empty, or whitespace strings.
  - **Previous state (v1.0.0):** No whitespace validation was performed on constructor parameters or init properties.
  - **Current state (v2.0.0):** `ArgumentException.ThrowIfNullOrWhiteSpace` enforced on `AuditActor.Id`, `AuditResource.Type`, `AuditResource.Id`, and `AuditContext.Source`.
  - **Affected:** Integrations passing empty strings to represent placeholder resources or anonymous actors without using the `AuditActor.Anonymous` singleton.
  - **Impact:** Runtime `ArgumentException`.
  - **Migration:** Ensure resource, actor, and source identifiers contain valid non-empty strings, or leverage `AuditActor.Anonymous`.

- **Required `idempotency_key` Column in SQL Persistence Adapters**:
  - **What changed:** Relational storage adapters (`PostgreSqlAuditStore`, `SqlServerAuditStore`, `MySqlAuditStore`, `OracleAuditStore`, `SqliteAuditStore`) incorporated `idempotency_key` into their `INSERT` and `SELECT` statements.
  - **Previous state (v1.0.0):** Database schema did not include or query `idempotency_key`.
  - **Current state (v2.0.0):** All SQL statements reference and populate `idempotency_key`.
  - **Affected:** All production databases created with v1.0.0 DDL scripts.
  - **Impact:** Immediate failure during `AppendAsync` and `QueryAsync` due to SQL error (*column "idempotency_key" does not exist*).
  - **Migration:** Execute schema migration DDL `ALTER TABLE audit_records ADD idempotency_key VARCHAR(128) NULL;` on existing databases prior to upgrading to v2.0.0.

- **Unique Constraint for Fork Prevention in Forensic Hash Chain**:
  - **What changed:** Added unique constraint and filtered index on `(tenant_id, previous_hash)` across relational database migrations.
  - **Previous state (v1.0.0):** No uniqueness constraint existed for previous hash pointers.
  - **Current state (v2.0.0):** Migrations apply filtered unique index (`WHERE previous_hash IS NOT NULL`).
  - **Affected:** Existing databases containing orphan records or hash collisions in `previous_hash`.
  - **Impact:** Migration script failure if duplicate previous hashes exist; potential unique constraint violations (`1062` / `2601`) if two concurrent uncoordinated writers attempt to commit the same predecessor without outbox or channel buffering.
  - **Migration:** Deduplicate historical collisions before applying the unique index constraint.

- **SQL Identifier Validation in Storage Provider Options**:
  - **What changed:** `Table` and `Schema` properties on storage options (`DapperAuditStoreOptions`, `PostgreSqlAuditStoreOptions`, `SqlServerAuditStoreOptions`, etc.) validate input against regex `^[a-zA-Z_][a-zA-Z0-9_]*$`.
  - **Previous state (v1.0.0):** Any string was accepted without sanitization.
  - **Current state (v2.0.0):** Strings containing dots, brackets, spaces, or special characters are rejected.
  - **Affected:** Consumers configuring compound identifiers such as `options.Table = "custom.audit_records"`.
  - **Impact:** `ArgumentException` during options initialization.
  - **Migration:** Specify schema in `options.Schema` and table name in `options.Table` independently.

- **Major Dependency Upgrades for Infrastructure & Data Connectors**:
  - **What changed:** Core infrastructure and connector dependencies upgraded to latest major versions: `Microsoft.Data.SqlClient` (5.2.2 → 7.0.2), `Npgsql` (9.0.4 → 10.0.3), `Microsoft.Data.Sqlite` (9.0.2 → 10.0.11), `Microsoft.Extensions.DependencyInjection` (9.0.7 → 10.0.11), and introduction of `Polly.Core` (8.7.0).
  - **Previous state (v1.0.0):** Compiled against .NET 8 / .NET 9 dependencies.
  - **Current state (v2.0.0):** Optimized for .NET 10 and modern database driver standards.
  - **Affected:** Host applications with strict driver version locking or legacy connection strings (e.g. strict TLS encryption defaults in SqlClient v7).
  - **Impact:** Dependency resolution conflicts (diamond dependencies) or behavioral changes in connection negotiation.
  - **Migration:** Upgrade host application references and review connection strings against driver requirements.

- **Native AOT and Trimming Compatibility Boundaries**:
  - **What changed:** Removed global `<IsAotCompatible>` directive from `Directory.Build.props` (due to `Reflection.Emit` constraints in Dapper 2.1.79) and explicitly scoped Native AOT guarantees.
  - **Previous state (v1.0.0):** `<IsAotCompatible>` was enabled globally across all packages.
  - **Current state (v2.0.0):** Native AOT is guaranteed for Abstractions, Core, Analyzers, SQLite, and PostgreSQL; EF Core and MongoDB are explicitly excluded due to driver runtime reflection constraints.
  - **Affected:** Applications compiled with Native AOT (`PublishAot=true`) consuming EF Core or MongoDB for audit persistence.
  - **Impact:** Compilation/trimming warnings and AOT publication failures.
  - **Migration:** Use AOT-certified storage adapters (PostgreSQL, SQLite, or Dapper with source generation) for Native AOT publish deployments.

---

### ✨ Added
- **Roslyn Diagnostic Analyzers (`EricksonLopez.Auditing.Analyzers`)**:
  - Code analysis rules and diagnostic analyzers targeting `netstandard2.0` enforcing audit immutability, required properties, and compliance standards at compile time.
- **Enterprise Cloud KMS Key Provider (`EricksonLopez.Auditing.AzureKeyVault`)**:
  - `AzureKeyVaultIntegrityProvider` — Azure Key Vault secret client implementation of `IAuditIntegrityProvider` and `IAuditCryptoKeyProvider` for HSM-backed audit key management and rotation.
- **Transactional Outbox Persistence (`EricksonLopez.Auditing.Outbox`)**:
  - `OutboxAuditStore` — transactional outbox storage decorator implementing `IAuditStore` for atomic business transaction staging and decoupled asynchronous dispatch.
  - `IOutboxMessageService` — contract for outbox message persistence and queue delivery.
- **Core Pipeline Decorators & Buffers (`EricksonLopez.Auditing`)**:
  - `BufferedAuditStoreDecorator` — high-throughput channel-buffered store with `BufferedAuditStoreOptions` utilizing `System.Threading.Channels` for asynchronous batch draining without blocking request threads.
  - `ResilientAuditStoreDecorator` — resilience interceptor enforcing `AuditFailureBehavior` (`FailClosed`, `FailOpen`, `Deferred`) and rethrowing exceptions on `CriticalActionCodes`.
  - `IntegrityAuditStoreDecorator` — automated HMAC-SHA256 predecessor hash chaining decorator wrapping any `IAuditStore`.
  - `AuditLogger<T>` — generic application-level audit logging facade implementing `IAuditLogger<T>`.
  - `AuditRecordBuilder` — fluent builder for fluent `AuditRecord` creation.
- **Observability Decorators (`EricksonLopez.Auditing.OpenTelemetry`)**:
  - `OpenTelemetryAuditStoreDecorator` — transparent decorator capturing spans and telemetry metrics for all `IAuditStore` operations.
  - `OpenTelemetryAuditIntegrityVerifierDecorator` — decorator tracing cryptographic chain verification operations.
- **Domain Abstractions & SPIs (`EricksonLopez.Auditing.Abstractions`)**:
  - `TenantId` — strongly-typed readonly record struct value object with implicit string conversions and validation.
  - `AuditCursorToken` — opaque base64 keyset pagination cursor token encoding and parsing (`Create`, `TryParse`).
  - `IAuditLogger` & `IAuditLogger<T>` — standardized facade interface for application audit event dispatch.
  - `IAuditTimeProvider` & `AmbientAuditTimeProvider` — ambient and injectable abstraction for deterministic timestamp generation.
  - `IAuditCryptoKeyProvider` — cryptographic key provider interface with support for GDPR Article 17 Crypto-Shredding (`ShredKeyAsync`).
  - `IAuditHashAlgorithm` & `HmacSha256AuditHashAlgorithm` — pluggable cryptographic hash algorithm SPI.
  - `StreamAsync` — default interface method (DIM) on `IAuditStore` enabling reactive asynchronous streaming of audit records via `IAsyncEnumerable<AuditRecord>`.
- **Showcase & Quality Enforcement**:
  - Interactive Showcase Level 11 (`Level11_ComprehensiveApiCoverage.cs`) for comprehensive 100% public API verification.
  - Benchmark Regression Gate workflow (`benchmark-regression-gate.yml`) with automated assertion script (`scripts/verify-benchmark-gate.ps1`) enforcing 0-byte allocations on hot paths and maximum 5% latency regression.
  - Stryker mutation testing configurations for new packages: `stryker-analyzers-config.json`, `stryker-azurekeyvault-config.json`, and `stryker-outbox-config.json`.
  - Architecture Decision Records: [ADR-0011](docs/decisions/adr-0011-transactional-outbox-and-channel-buffering.md) and [ADR-0012](docs/decisions/adr-0012-cloud-kms-and-gdpr-crypto-shredding.md).

---

## [1.0.0] — 2026-08-26

### Added
- **Core Domain & Model Primitives (`EricksonLopez.Auditing.Abstractions`)**:
  - `AuditRecord` — immutable, canonical evidence record capturing `Actor`, `Action`, `Resource`, `Context`, `Outcome`, `Changes`, and cryptographic hashes.
  - `AuditActor` — typed, immutable actor representation with `AuditActorType` discriminator (`User`, `SystemProcess`, `Service`, `ScheduledJob`, `Integration`, `Anonymous`) and predefined singletons (`AuditActor.Anonymous`, `AuditActor.System`).
  - `AuditAction` — extensible readonly record struct for action codes with standard predefined operations (`Create`, `Update`, `Delete`, `Read`, `Approve`, `Reject`, `Login`, `Logout`, `Export`, `Download`, `Send`, `Cancel`, `Restore`).
  - `AuditResource` — target entity representation with `Type`, `Id`, and optional `DisplayName`.
  - `AuditContext` — execution context metadata including `TenantId`, `Source`, `CorrelationId`, `CausationId`, `RequestId`, `IpAddress`, `UserAgent`, and reserved `SystemTenantId`.
  - `AuditOutcome` — operational result enumeration (`Success`, `Failure`, `Denied`, `Cancelled`, `Partial`).
  - `AuditChange` — field-level delta tracking with `Field`, `OldValue`, `NewValue`, and `IsRedacted` flag.
  - `IAuditStore` — primary asynchronous append-only persistence SPI (`AppendAsync`, `AppendBatchAsync`, `QueryAsync`).
  - `AuditQuery` & `AuditQueryResult` — query filter specification featuring $O(1)$ Keyset cursor seek pagination (`ContinuationToken`, `PageSize`, `HasMore`, `NextPageToken`).
  - `IAuditIntegrityProvider` & `IAuditIntegrityVerifier` — cryptographic key retrieval and audit chain verification abstractions.

- **Core Engine & Middleware Pipeline (`EricksonLopez.Auditing`)**:
  - `AuditId.NewId()` — monotonic, timestamp-ordered UUID generation according to RFC 9562 (UUIDv7).
  - `AuditScope` — ambient execution context manager with `AsyncLocal<T>` propagation, hierarchical metadata enrichment (`WithMetadata`), and parent scope restoration on disposal.
  - `AuditConfiguration` — centralized runtime configuration supporting `DefaultFailureBehavior` (`FailClosed`, `FailOpen`, `Deferred`), `CriticalActionCodes`, `GlobalFieldDenylist`, and batch queue settings.
  - `AuditSensitivityPipeline` — sensitive field sanitizer with global denylist enforcement, PII redaction (`AuditChange.Redacted`), and SHA-256 one-way hashing helper (`HashValue`).
  - `HmacAuditIntegrityService` — HMAC-SHA256 cryptographic chain builder and record verifier.
  - `AuditJsonContext` — source-generated Native AOT-safe JSON serialization metadata.
  - `AddAuditing()` — dependency injection extensions for service registration.

- **Enterprise Database Storage Adapters**:
  - `EricksonLopez.Auditing.PostgreSql` — PostgreSQL 14+ adapter with Row-Level Security (RLS) tenant isolation, monthly partitioning support, Dapper, and HMAC chain verification.
  - `EricksonLopez.Auditing.SqlServer` — SQL Server / Azure SQL adapter with `SESSION_CONTEXT` and Security Policy RLS tenant isolation, Dapper, and HMAC chain verification.
  - `EricksonLopez.Auditing.Sqlite` — SQLite adapter with WAL mode, parameterized SQL, and Dapper for edge and local testing.
  - `EricksonLopez.Auditing.MySql` — MySQL 8.0+ and MariaDB adapter with `MySqlConnector` and Dapper.
  - `EricksonLopez.Auditing.Oracle` — Oracle Database 19c/21c/23ai adapter with Virtual Private Database (VPD) multi-tenancy and Dapper.
  - `EricksonLopez.Auditing.MongoDb` — MongoDB 6.0+ document-oriented append-only store with tenant indexing.
  - `EricksonLopez.Auditing.EntityFrameworkCore` — Relational EF Core store with `AuditDbContext` and shadow property mapping.
  - `EricksonLopez.Auditing.Dapper` — Database-agnostic ANSI SQL adapter for any ADO.NET `DbConnection`.

- **Observability & Testing Packages**:
  - `EricksonLopez.Auditing.OpenTelemetry` — distributed tracing with `AuditActivitySource` (`audit.append`, `audit.batch`, `audit.query`) and OpenTelemetry meter metrics.
  - `EricksonLopez.Auditing.Testing` — testing harness with `InMemoryAuditStore`, `AuditRecordBuilder`, and `TestAuditIntegrityProvider` for unit testing consumers.

- **Architecture, Documentation & Quality Gates**:
  - Complete English documentation across `/docs/`: Quickstart, Getting Started, Cookbook, API Reference, Architecture, Best Practices, FAQ, Migration Guide, Performance Guide, Troubleshooting, and CI/CD Quality Gates.
  - 10 Architecture Decision Records (ADRs) under `docs/decisions/` capturing foundational design invariants.
  - Interactive multi-level showcase sample application in `samples/EricksonLopez.Auditing.Showcase/`.
  - Multi-target framework compilation across `net8.0`, `net9.0`, and `net10.0` with full Native AOT and trim analyzers enabled.
  - Open-source community health standards: `LICENSE`, `CONTRIBUTING.md`, `CODE_OF_CONDUCT.md`, `SECURITY.md`, `SUPPORT.md`, `.github/CODEOWNERS`, issue templates, and PR templates.

---

[Unreleased]: https://github.com/ericksonlopezf/dotnet-auditing/compare/v2.0.0...HEAD
[2.0.0]: https://github.com/ericksonlopezf/dotnet-auditing/compare/v1.0.0...v2.0.0
[1.0.0]: https://github.com/ericksonlopezf/dotnet-auditing/releases/tag/v1.0.0
