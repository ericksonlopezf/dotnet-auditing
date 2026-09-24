<!-- Copyright © Erickson Lopez. MIT License. -->
# Contributing to EricksonLopez.Auditing

Thank you for your interest in contributing to **EricksonLopez.Auditing**! We welcome contributions from the community to help make this framework the most reliable, secure, and performant audit trail ecosystem for .NET.

Please read this document carefully before submitting issues or pull requests.

---

## Code of Conduct

All contributors and maintainers are expected to adhere to the [Code of Conduct](CODE_OF_CONDUCT.md). Please report any unacceptable behavior to [ericksonlopezf@gmail.com](mailto:ericksonlopezf@gmail.com).

---

## Prerequisites

To build and run tests locally, ensure you have the following installed:

* [.NET SDK 8.0+](https://dotnet.microsoft.com/download) (The solution multi-targets `net8.0`, `net9.0`, and `net10.0`).
* [Git](https://git-scm.com/)
* [Docker Desktop](https://www.docker.com/) / Docker engine (Optional, required only for running integration tests against database engines with Testcontainers).

---

## Repository Structure

```text
dotnet-auditing/
├── src/
│   ├── EricksonLopez.Auditing.Abstractions/       # Foundation contracts, SPI & HMAC service (Zero dependencies)
│   ├── EricksonLopez.Auditing/                    # Core engine, UUIDv7, AsyncLocal scope, sensitivity pipeline
│   ├── EricksonLopez.Auditing.Analyzers/          # Roslyn diagnostic analyzers & code fixes (netstandard2.0)
│   ├── EricksonLopez.Auditing.AzureKeyVault/      # Azure Key Vault cryptographic KMS provider
│   ├── EricksonLopez.Auditing.Outbox/             # Transactional outbox persistence decorator & contracts
│   ├── EricksonLopez.Auditing.Testing/            # In-memory store, test doubles, record builders
│   ├── EricksonLopez.Auditing.Dapper/             # Generic ANSI SQL adapter via Dapper
│   ├── EricksonLopez.Auditing.PostgreSql/         # PostgreSQL adapter with Row-Level Security (RLS)
│   ├── EricksonLopez.Auditing.SqlServer/          # SQL Server adapter with SESSION_CONTEXT security policy
│   ├── EricksonLopez.Auditing.MySql/              # MySQL adapter with session context variables
│   ├── EricksonLopez.Auditing.Oracle/             # Oracle Database adapter with DBMS_SESSION (VPD)
│   ├── EricksonLopez.Auditing.Sqlite/             # SQLite adapter for edge, local, and desktop
│   ├── EricksonLopez.Auditing.EntityFrameworkCore/# EF Core adapter with dedicated AuditDbContext
│   ├── EricksonLopez.Auditing.MongoDb/            # MongoDB adapter with BSON append-only persistence
│   └── EricksonLopez.Auditing.OpenTelemetry/      # Semantic ActivitySource and metrics instrumentation
├── tests/
│   ├── Common/                                    # Shared fake ADO.NET doubles (FakeDb)
│   ├── EricksonLopez.Auditing.Tests/              # Core engine, scope, HMAC, and decorator unit tests
│   ├── EricksonLopez.Auditing.Abstractions.Tests/ # Model, value object, and contract unit tests
│   ├── EricksonLopez.Auditing.Analyzers.Tests/    # Roslyn analyzer & code fix verification tests
│   ├── EricksonLopez.Auditing.Dapper.Tests/       # Dapper query formatting & parameter binding tests
│   ├── EricksonLopez.Auditing.EntityFrameworkCore.Tests/ # EF Core change interception & store tests
│   ├── EricksonLopez.Auditing.PostgreSql.Tests/   # PostgreSQL RLS & query unit tests (FakeDb)
│   ├── EricksonLopez.Auditing.SqlServer.Tests/    # SQL Server SESSION_CONTEXT unit tests (FakeDb)
│   ├── EricksonLopez.Auditing.MySql.Tests/        # MySQL session variable unit tests (FakeDb)
│   ├── EricksonLopez.Auditing.Oracle.Tests/       # Oracle DBMS_SESSION unit tests (FakeDb)
│   ├── EricksonLopez.Auditing.Sqlite.Tests/       # SQLite unit tests (FakeDb)
│   ├── EricksonLopez.Auditing.MongoDb.Tests/      # MongoDB BSON mapping & query unit tests
│   ├── EricksonLopez.Auditing.OpenTelemetry.Tests/# OpenTelemetry tracing & meter unit tests
│   ├── EricksonLopez.Auditing.Testing.Tests/      # In-memory store & builder unit tests
│   ├── EricksonLopez.Auditing.AotSmokeTest/       # Native AOT PublishAot runtime smoke test
│   └── EricksonLopez.Auditing.IntegrationTests/   # Testcontainers integration tests (real database engines)
├── benchmarks/
│   └── EricksonLopez.Auditing.Benchmarks/        # BenchmarkDotNet performance test suite & regression baseline
├── samples/
│   └── EricksonLopez.Auditing.Showcase/          # Reference implementation with 12 progressive levels (00-11)
└── docs/                                         # Technical documentation, guides, and ADRs
```

---

## Development Workflow

### 1. Clone Repository

```bash
git clone https://github.com/ericksonlopezf/dotnet-auditing.git
cd dotnet-auditing
```

### 2. Restore Dependencies

```bash
dotnet restore EricksonLopez.Auditing.slnx
```

### 3. Build Solution

The solution enforces `TreatWarningsAsErrors=true` and `AnalysisLevel=latest-recommended`:

```bash
# Debug build
dotnet build EricksonLopez.Auditing.slnx

# Release build
dotnet build EricksonLopez.Auditing.slnx -c Release
```

### 4. Run Unit Tests

Unit tests are modularized 1:1 per package, fast, isolated, and require zero external infrastructure:

```bash
# Run all unit tests across net8.0, net9.0, net10.0
dotnet test EricksonLopez.Auditing.slnx --filter "Category!=Integration"

# Run unit tests for a specific package (e.g. Core)
dotnet test tests/EricksonLopez.Auditing.Tests/EricksonLopez.Auditing.Tests.csproj

# Run unit tests with code coverage collection
dotnet test EricksonLopez.Auditing.slnx --filter "Category!=Integration" --collect:"XPlat Code Coverage"
```

### 5. Run Integration Tests (Requires Docker)

Integration tests use Testcontainers to spin up ephemeral database instances:

```bash
# Run only SQLite integration tests (No Docker required)
dotnet test tests/EricksonLopez.Auditing.IntegrationTests/EricksonLopez.Auditing.IntegrationTests.csproj --filter "FullyQualifiedName~Sqlite"

# Run all integration tests (PostgreSQL, SQL Server, MySQL, Oracle, MongoDB)
dotnet test tests/EricksonLopez.Auditing.IntegrationTests/EricksonLopez.Auditing.IntegrationTests.csproj
```

### 6. Run Benchmarks & Quality Gates

```bash
# Run BenchmarkDotNet performance suite
dotnet run --project benchmarks/EricksonLopez.Auditing.Benchmarks/EricksonLopez.Auditing.Benchmarks.csproj -c Release

# Run mutation testing against Core
dotnet stryker --config-file stryker-config.json
```

### 7. Run Executable Showcase

```bash
dotnet run --project samples/EricksonLopez.Auditing.Showcase/EricksonLopez.Auditing.Showcase.csproj --framework net9.0 -- --all
```

---

## Branching & Commit Conventions

### Branch Strategy

* `main`: Production-ready release branch. Protected.
* `develop`: Main integration branch for upcoming features and fixes.
* Feature/Fix branches: Create branches off `develop` named according to purpose:
  * `feat/<feature-name>`
  * `fix/<bug-description>`
  * `docs/<doc-update>`
  * `refactor/<refactoring-name>`

### Commit Messages (Conventional Commits)

Please follow the [Conventional Commits](https://www.conventionalcommits.org/) specification:

```text
<type>(<scope>): <short summary>

[optional body]

[optional footer(s)]
```

**Types:**
* `feat`: A new feature or public API extension.
* `fix`: A bug fix.
* `docs`: Documentation updates.
* `perf`: A code change that improves performance.
* `refactor`: Code restructuring without changing behavior.
* `test`: Adding or updating test suites.
* `chore`: Build scripts, CI workflow changes, dependency updates.

**Examples:**
* `feat(abstractions): add CorrelationId filter to AuditQuery`
* `fix(sqlite): register IAuditIntegrityVerifier in DI container`
* `docs(architecture): add sequence flow for HMAC verification`

---

## Quality Gates & Coding Standards

1. **Native AOT & Trimming**:
   - Zero dynamic reflection in public serialization pathways (`AuditJsonContext` source generation).
   - Core and abstractions are validated for Native AOT and trimming (`EnableTrimAnalyzer=true`).
2. **Immutability & Thread-Safety**:
   - `AuditRecord`, `AuditContext`, `AuditActor`, `AuditResource`, `AuditChange` are immutable records.
   - `AuditScope` manages `AsyncLocal<T>` safely across async execution contexts.
3. **Multi-Tenant Security**:
   - Every database adapter must set session context/RLS variables before issuing commands.
   - Batch insertions must enforce single-tenant homogeneity (`All records in a batch must belong to the same tenant`).
4. **Code Coverage & Mutation Score**:
   - New functionality must include unit tests achieving ≥99% line coverage.
   - Mutation testing score threshold: `≥95%` break threshold across all packages, `100%` for core cryptographic services.
5. **Benchmark Regression Gate**:
   - Hot path combinators enforce 0 B heap allocations.
   - Mean latency regression must not exceed 5% vs baseline.

---

## Pull Request Process

1. Fork the repository and create your branch from `develop`.
2. Ensure code compiles cleanly with zero warnings (`dotnet build -c Release`).
3. Ensure all unit tests pass (`dotnet test EricksonLopez.Auditing.slnx --filter "Category!=Integration"`).
4. Update or add documentation in `/docs/` and `README.md` if public APIs or behaviors changed.
5. Submit your PR targeting the `develop` branch using the provided [Pull Request Template](.github/PULL_REQUEST_TEMPLATE.md).
