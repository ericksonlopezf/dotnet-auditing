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
│   ├── EricksonLopez.Auditing.UnitTests/         # In-memory fast unit tests (100% code coverage)
│   └── EricksonLopez.Auditing.IntegrationTests/  # Testcontainers integration tests with real engines
├── benchmarks/
│   └── EricksonLopez.Auditing.Benchmarks/        # BenchmarkDotNet performance test suite
├── samples/
│   └── EricksonLopez.Auditing.Showcase/          # Reference implementation with 11 progressive levels
└── docs/                                         # Technical documentation and ADRs
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

Unit tests are fast, isolated, and require zero external infrastructure:

```bash
# Run unit tests across net8.0, net9.0, net10.0
dotnet test tests/EricksonLopez.Auditing.UnitTests/EricksonLopez.Auditing.UnitTests.csproj

# Run unit tests with code coverage collection
dotnet test tests/EricksonLopez.Auditing.UnitTests/EricksonLopez.Auditing.UnitTests.csproj --collect:"XPlat Code Coverage"
```

### 5. Run Integration Tests (Requires Docker)

Integration tests use Testcontainers to spin up ephemeral database instances:

```bash
# Run only SQLite integration tests (No Docker required)
dotnet test tests/EricksonLopez.Auditing.IntegrationTests/EricksonLopez.Auditing.IntegrationTests.csproj --filter "FullyQualifiedName~Sqlite"

# Run all integration tests (PostgreSQL, SQL Server, MySQL, Oracle, MongoDB)
dotnet test tests/EricksonLopez.Auditing.IntegrationTests/EricksonLopez.Auditing.IntegrationTests.csproj
```

### 6. Run Benchmarks

```bash
dotnet run --project benchmarks/EricksonLopez.Auditing.Benchmarks/EricksonLopez.Auditing.Benchmarks.csproj -c Release
```

### 7. Run Executable Showcase

```bash
dotnet run --project samples/EricksonLopez.Auditing.Showcase/EricksonLopez.Auditing.Showcase.csproj --framework net9.0 -- --all
```

---

## Branching & Commit Conventions

### Branch Strategy

* `main`: Production-ready release branch.
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
   * All code in `src/` must be 100% Native AOT compatible (`<IsAotCompatible>true</IsAotCompatible>`).
   * No runtime dynamic reflection or unconstrained generic serialization. Use source-generated `JsonSerializerContext` (`AuditJsonContext`).
2. **Immutability & Thread-Safety**:
   * `AuditRecord`, `AuditContext`, `AuditActor`, `AuditResource`, `AuditChange` are immutable records.
   * `AuditScope` manages `AsyncLocal<T>` safely across async execution contexts.
3. **Multi-Tenant Security**:
   * Every database adapter must set session context/RLS variables before issuing commands.
   * Batch insertions must enforce single-tenant homogeneity (`All records in a batch must belong to the same tenant`).
4. **Code Coverage & Mutation Score**:
   * New functionality must include unit tests achieving 100% line coverage in core and cryptographic components.
   * Mutation testing score threshold: $\ge 95\%$ break threshold across all packages, $100\%$ for core cryptographic services.

---

## Pull Request Process

1. Fork the repository and create your branch from `develop`.
2. Ensure code compiles cleanly with zero warnings (`dotnet build -c Release`).
3. Ensure all unit tests pass (`dotnet test tests/EricksonLopez.Auditing.UnitTests`).
4. Update or add documentation in `/docs/` and `README.md` if public APIs or behaviors changed.
5. Submit your PR targeting the `develop` branch using the provided [Pull Request Template](.github/PULL_REQUEST_TEMPLATE.md).
