# ADR-0005: Zero-Reflection Native AOT & Trimming Serialization

## Context

Cloud-native containerized applications and edge computing runtimes increasingly require fast cold start times and minimal memory footprints via .NET Native AOT compilation. Traditional JSON serializers relying on runtime reflection (`System.Reflection.Emit`) produce compiler trimming warnings and fail during Native AOT execution.

## Decision

All JSON serialization for `AuditChange` lists and metadata across core and storage adapters (PostgreSQL JSONB, SQL Server JSON, MySQL JSON, Oracle CLOB, SQLite TEXT) utilizes source-generated `JsonSerializerContext` (`AuditJsonContext`). Dynamic runtime reflection is strictly forbidden across all public and internal paths.

## Consequences

### Positive
* 100% Native AOT and IL trimming compatibility with zero compiler warnings (`IsAotCompatible=true`, `EnableTrimAnalyzer=true`).
* Substantially lower cold-start latency and reduced working set memory consumption.

### Negative / Trade-offs
* All serializable models and DTOs must be explicitly registered with source generator attributes at compile time.
