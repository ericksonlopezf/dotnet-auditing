# EricksonLopez.Auditing — Test Suite Documentation

This test suite guarantees the reliability, immutability, multi-tenant isolation, and cryptographic security of `EricksonLopez.Auditing` across all domain abstractions and persistence adapters.

---

## 1. Project Structure

```text
tests/
├── Common/                                             # Shared ADO.NET fake test doubles & helpers
│   ├── FakeDbCommand.cs
│   ├── FakeDbConnection.cs
│   ├── FakeDbDataReader.cs
│   ├── FakeDbDataReaderFactory.cs
│   ├── FakeDbParameter.cs
│   ├── FakeDbParameterCollection.cs
│   └── FakeDbTransaction.cs
├── EricksonLopez.Auditing.Abstractions.Tests/          # AuditId, UUIDv7, value objects, SPI contracts
├── EricksonLopez.Auditing.Tests/                       # Core engine, AuditScope, HMAC chain, sensitivity pipeline
├── EricksonLopez.Auditing.Dapper.Tests/                # Dapper raw SQL adapter unit tests
├── EricksonLopez.Auditing.EntityFrameworkCore.Tests/   # EF Core change interception & store unit tests
├── EricksonLopez.Auditing.PostgreSql.Tests/            # PostgreSQL provider unit tests (FakeDb)
├── EricksonLopez.Auditing.SqlServer.Tests/             # SQL Server provider unit tests (FakeDb)
├── EricksonLopez.Auditing.Sqlite.Tests/                # SQLite provider unit tests (FakeDb)
├── EricksonLopez.Auditing.MySql.Tests/                 # MySQL provider unit tests (FakeDb)
├── EricksonLopez.Auditing.Oracle.Tests/                # Oracle provider unit tests (FakeDb)
├── EricksonLopez.Auditing.MongoDb.Tests/               # MongoDB BSON mapping & store unit tests
├── EricksonLopez.Auditing.OpenTelemetry.Tests/         # ActivitySource & tracing metrics unit tests
├── EricksonLopez.Auditing.Testing.Tests/               # In-memory test doubles & test builders
├── EricksonLopez.Auditing.AotSmokeTest/                # Native AOT PublishAot runtime smoke test
└── EricksonLopez.Auditing.IntegrationTests/            # Real container tests (PostgreSQL, SQL Server, MySQL, Oracle, SQLite)
```

---

## 2. Test Execution

### 2.1 Unit Tests (Multi-Target: .NET 8, .NET 9, .NET 10)

Unit tests require no external databases or Docker and execute in parallel:

```bash
# Run all unit tests across the entire solution
dotnet test EricksonLopez.Auditing.slnx --filter "Category!=Integration"

# Run with code coverage collection
dotnet test EricksonLopez.Auditing.slnx --filter "Category!=Integration" --collect:"XPlat Code Coverage"
```

### 2.2 Integration Tests (Requires Docker / Testcontainers)

Integration tests use **Testcontainers** to spin up ephemeral database instances (PostgreSQL, SQL Server, MySQL, Oracle, MongoDB) plus local disk SQLite:

```bash
# Run local SQLite integration tests (does not require Docker)
dotnet test tests/EricksonLopez.Auditing.IntegrationTests/EricksonLopez.Auditing.IntegrationTests.csproj --filter "FullyQualifiedName~Sqlite"

# Run complete integration test suite (requires active Docker engine)
dotnet test tests/EricksonLopez.Auditing.IntegrationTests/EricksonLopez.Auditing.IntegrationTests.csproj
```

---

## 3. Mutation Testing (Stryker.NET)

Assertion quality and test resilience are verified through isolated Stryker.NET matrix execution:

```bash
# Run Stryker against Core package
dotnet stryker --config-file stryker-config.json

# Run Stryker against PostgreSql package
dotnet stryker --config-file stryker-postgresql-config.json
```

### Mutation Policy:
- **Threshold**: `high: 100%`, `low: 98%`, `break: 95%`.
- **Core & Cryptography**: 100.0% Mutation Score verified.
- **Equivalent Mutants**: Managed according to [ADR-0010](../docs/decisions/adr-0010-stryker-equivalent-mutants-and-compiler-exclusions.md).

---

## 4. Architectural Patterns Used in Testing

- **Modular Test Isolation**: Dedicated 1:1 test project per package avoiding cross-package test interference.
- **Fluent Data Builders**: `AuditRecordBuilder` in `EricksonLopez.Auditing.Testing` provides concise test data creation.
- **Humble Object Pattern**: Pure domain logic (`HmacAuditIntegrityService`, `AuditSensitivityPipeline`) is fully tested without I/O.
- **ADO.NET Fake Doubles**: Shared lightweight fake ADO.NET connection and command abstractions validate generated SQL in microsecond execution times.
