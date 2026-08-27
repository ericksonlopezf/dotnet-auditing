# ADR-0009: Modular Test Suite & Mutation Testing Isolation

## Status
**Accepted**

## Date
2026-08-26

## Context
Previously, `EricksonLopez.Auditing` maintained a single monolithic unit test project (`EricksonLopez.Auditing.UnitTests.csproj`) containing unit tests for all 12 packages (Core, Abstractions, Dapper, PostgreSQL, SQL Server, MySQL, SQLite, Oracle, EF Core, MongoDb, OpenTelemetry, Testing).

When executing Stryker.NET mutation testing across the matrix, every package configuration executed the entire monolithic test suite. This introduced:
1. Significant test execution overhead and CI bottleneck.
2. Unintended cross-package coupling in test execution.
3. Obscured test failure attribution during mutation analysis.

## Decision
1. Deconstruct `EricksonLopez.Auditing.UnitTests` into 12 dedicated, isolated test projects matching each production package 1:1:
   - `tests/EricksonLopez.Auditing.Abstractions.Tests`
   - `tests/EricksonLopez.Auditing.Tests`
   - `tests/EricksonLopez.Auditing.Dapper.Tests`
   - `tests/EricksonLopez.Auditing.EntityFrameworkCore.Tests`
   - `tests/EricksonLopez.Auditing.PostgreSql.Tests`
   - `tests/EricksonLopez.Auditing.SqlServer.Tests`
   - `tests/EricksonLopez.Auditing.Sqlite.Tests`
   - `tests/EricksonLopez.Auditing.MySql.Tests`
   - `tests/EricksonLopez.Auditing.Oracle.Tests`
   - `tests/EricksonLopez.Auditing.MongoDb.Tests`
   - `tests/EricksonLopez.Auditing.OpenTelemetry.Tests`
   - `tests/EricksonLopez.Auditing.Testing.Tests`
2. Extract common test doubles (e.g. `FakeDbConnection`, `FakeDbDataReaderFactory`) into `tests/Common`.
3. Update all 12 `stryker-*.json` files to target exclusively their matching isolated test project.

## Consequences
### Positive
- Strict isolation of dependencies and test executions.
- Stryker mutation runs execute significantly faster and in complete isolation.
- Clear, unambiguous mapping between source changes and test coverage.

### Negative
- Additional `.csproj` files to manage in the solution (`EricksonLopez.Auditing.slnx`).
