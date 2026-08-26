# Changelog

All notable changes to the `EricksonLopez.Auditing` ecosystem will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [Unreleased]

---

## [1.0.0] — 2026-08-26

### Added
- **Core Domain & Model Primitives (`EricksonLopez.Auditing.Abstractions`)**:
  - `AuditRecord` — immutable, canonical evidence record capturing `Actor`, `Action`, `Resource`, `Context`, `Outcome`, `Changes`, and cryptographic hashes.
  - `AuditActor` — typed, immutable actor representation with `AuditActorType` discriminator (`User`, `SystemProcess`, `Service`, `ScheduledJob`, `Integration`, `Anonymous`) and predefined singletons (`AuditActor.Anonymous`, `AuditActor.System`).
  - `AuditAction` — extensible readonly record struct for action codes with standard predefined operations (`Create`, `Update`, `Delete`, `Read`, `Approve`, `Reject`, `Login`, `Logout`, `Export`, `Download`, `Send`, `Cancel`, `Restore`).
  - `AuditResource` — target entity representation with `Type`, `Id`, and optional `DisplayName`.
  - `AuditContext` — execution context metadata including `TenantId`, `Source`, `CorrelationId`, `CausationId`, `RequestId`, `IpAddress`, `UserAgent`, and reserved `SystemTenantId`.
  - `AuditOutcome` — operational result enumeration (`Success`, `Failure`, `Denied`, `Pending`).
  - `AuditChange` — field-level delta tracking with `Field`, `OldValue`, `NewValue`, and `IsRedacted` flag.
  - `IAuditStore` — primary asynchronous append-only persistence SPI (`AppendAsync`, `AppendBatchAsync`, `QueryAsync`).
  - `AuditQuery` & `AuditQueryResult` — query filter specification featuring $O(1)$ Keyset cursor seek pagination (`AfterRecordId`, `PageSize`, `HasMore`, `NextCursorId`).
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
  - 8 Architecture Decision Records (ADRs) under `docs/decisions/` capturing foundational design invariants.
  - Interactive multi-level showcase sample application in `samples/EricksonLopez.Auditing.Showcase/`.
  - Multi-target framework compilation across `net8.0`, `net9.0`, and `net10.0` with full Native AOT and trim analyzers enabled.
  - Open-source community health standards: `LICENSE`, `CONTRIBUTING.md`, `CODE_OF_CONDUCT.md`, `SECURITY.md`, `SUPPORT.md`, `.github/CODEOWNERS`, issue templates, and PR templates.

---

[Unreleased]: https://github.com/ericksonlopezf/dotnet-auditing/compare/v1.0.0...HEAD
[1.0.0]: https://github.com/ericksonlopezf/dotnet-auditing/releases/tag/v1.0.0

