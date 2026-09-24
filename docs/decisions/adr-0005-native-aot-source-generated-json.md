<!-- Copyright © Erickson Lopez. MIT License. -->
# ADR-0005: Zero-Reflection Native AOT & Trimming Serialization

## Status
Accepted (Updated 2026-09-14)

## Date
2026-09-04

## Context

Cloud-native containerized applications and edge computing runtimes increasingly require fast cold start times and minimal memory footprints via .NET Native AOT compilation. Traditional JSON serializers relying on runtime reflection (`System.Reflection.Emit`) produce compiler trimming warnings and fail during Native AOT execution.

## Decision

All JSON serialization for `AuditChange` lists and metadata across core and storage adapters (PostgreSQL JSONB, SQL Server JSON, MySQL JSON, Oracle CLOB, SQLite TEXT) utilizes source-generated `JsonSerializerContext` (`AuditJsonContext`). Dynamic runtime reflection is strictly forbidden across all public and internal paths in the core packages.

**Scope of AOT Compatibility:**

| Package | AOT Compatible | Reason |
|---|:---:|---|
| `EricksonLopez.Auditing.Abstractions` | ✅ Yes | Zero external runtime dependencies |
| `EricksonLopez.Auditing` (Core) | ✅ Yes | Uses only AOT-safe Polly.Core and MS.Ext abstractions |
| `EricksonLopez.Auditing.Testing` | ✅ Yes | In-memory only, no reflection |
| `EricksonLopez.Auditing.Analyzers` | ✅ Yes | Compile-time only, targets `netstandard2.0` |
| `EricksonLopez.Auditing.PostgreSql` | ⚠️ Partial | Depends on Dapper v2.1.79 which uses `Reflection.Emit` |
| `EricksonLopez.Auditing.SqlServer` | ⚠️ Partial | Depends on Dapper v2.1.79 which uses `Reflection.Emit` |
| `EricksonLopez.Auditing.MySql` | ⚠️ Partial | Depends on Dapper v2.1.79 which uses `Reflection.Emit` |
| `EricksonLopez.Auditing.Oracle` | ⚠️ Partial | Depends on Dapper v2.1.79 which uses `Reflection.Emit` |
| `EricksonLopez.Auditing.Sqlite` | ⚠️ Partial | Depends on Dapper v2.1.79 which uses `Reflection.Emit` |
| `EricksonLopez.Auditing.Dapper` | ⚠️ Partial | Depends on Dapper v2.1.79 which uses `Reflection.Emit` |
| `EricksonLopez.Auditing.MongoDb` | ⚠️ Partial | MongoDB driver uses runtime reflection |
| `EricksonLopez.Auditing.EntityFrameworkCore` | ⚠️ Partial | EF Core has partial AOT support only |
| `EricksonLopez.Auditing.AzureKeyVault` | ⚠️ Partial | Azure SDK reflection usage |
| `EricksonLopez.Auditing.Outbox` | ✅ Yes | Depends only on AOT-compatible core |
| `EricksonLopez.Auditing.OpenTelemetry` | ✅ Yes | OTEL SDK is AOT-compatible |

**Note on `IsAotCompatible`:** The `IsAotCompatible=true` MSBuild property was intentionally **not set** globally in `Directory.Build.props` because Dapper v2.1.79 uses `System.Reflection.Emit`, which is incompatible with Native AOT. Individual packages that are fully AOT-compatible may opt into `IsAotCompatible=true` explicitly. Setting it on packages with Dapper would produce false-positive AOT warnings suppression.

## Consequences

### Positive
* Core domain packages (`Abstractions`, `Core`) achieve 100% Native AOT and IL trimming compatibility with zero compiler warnings (`EnableTrimAnalyzer=true`).
* Substantially lower cold-start latency and reduced working set memory consumption for deployments using only the core package.
* Serialization via source-generated `AuditJsonContext` eliminates reflection at serialization boundaries.

### Negative / Trade-offs
* Database adapter packages cannot claim `IsAotCompatible=true` until Dapper v3.x or an alternative mapping layer without `Reflection.Emit` is adopted (see GitHub issue #AOT).
* All serializable models and DTOs must be explicitly registered with source generator attributes at compile time.
* The repository badge "NativeAOT: Compatible" applies to the core packages only, not database adapters.
