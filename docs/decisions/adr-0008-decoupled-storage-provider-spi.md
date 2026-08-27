# ADR-0008: Decoupled Storage Provider Architecture (SPI Pattern)

## Context

Enterprise applications use varied persistence technologies (PostgreSQL, SQL Server, MySQL, Oracle, SQLite, MongoDB, EF Core). Bundling database drivers into the core library would bloat consumer dependency trees, introduce diamond dependency conflicts, and violate separation of concerns.

## Decision

Adopt the Service Provider Interface (SPI) architectural pattern. `EricksonLopez.Auditing.Abstractions` defines the canonical contracts (`IAuditStore`, `IAuditIntegrityVerifier`, `AuditRecord`) with zero external **database driver** dependencies (the only runtime dependency is `Microsoft.Extensions.DependencyInjection.Abstractions`, which is a standard .NET DI abstraction, not a database driver). Concrete storage adapters (`PostgreSql`, `SqlServer`, `MySql`, `Oracle`, `Sqlite`, `MongoDb`, `EntityFrameworkCore`, `Dapper`) are isolated standalone NuGet packages that implement the SPI.

## Consequences

### Positive
* Consumers only pull the exact database driver and dependencies required for their target environment.
* Enables third parties to implement custom `IAuditStore` adapters (e.g. AWS DynamoDB, Cassandra) without modifying core contracts.
* Keeps core abstractions ultra-lightweight and dependency-free.

### Negative / Trade-offs
* Maintaining 12 separate packages requires centralized package management (CPM) and disciplined multi-project build pipelines.
