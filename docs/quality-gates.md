# Quality Gates & Code Analysis Methodology — EricksonLopez.Mediator

`EricksonLopez.Mediator` enforces strict compile-time, Native AOT, and pipeline quality gates to guarantee performance, trimming safety, and architectural correctness.

---

## 1. Compile-Time Analyzer & Generator Gates

Compile-time verification is performed by `EricksonLopez.Mediator.Generator`. Violations break the build immediately in the developer's IDE:

| Diagnostic | Severity | Gate Description |
|:---:|:---:|---|
| **`ELM001`** | `Error` | Blocks build if any command or query lacks an implementing handler. |
| **`ELM002`** | `Error` | Blocks build if duplicate command handlers exist (enforces strict single-handler CQRS). |
| **`ELM003`** | `Error` | Blocks build if duplicate query handlers exist. |
| **`ELM004`** | `Error` | Blocks build if a handler `Handle` method has an invalid signature or return type. |
| **`ELM005`** | `Warning` | Warns if an open generic handler cannot be statically resolved to concrete types. |
| **`ELM006`** | `Warning` | Warns if a notification has no registered subscribers (dead event warning). |
| **`ELM007`** | `Error` | Blocks build if an open generic behavior has an unsupported signature or failing constraints. |
| **`ELM008`** | `Warning` | Flags non-deterministic behavior execution ordering conflicts. |
| **`ELM009`** | `Error` | Blocks build if a stream request (`IStreamRequest<T>`) lacks an implementing stream handler. |
| **`ELM010`** | `Error` | Blocks build if duplicate stream handlers exist for the same request type. |
| **`ELM011`** | `Error` | Blocks build if a stream handler `Handle` method does not return `IAsyncEnumerable<TResponse>`. |

---

## 2. Public API Analyzers Gate

The repository uses `Microsoft.CodeAnalysis.PublicApiAnalyzers` to prevent accidental breaking changes across releases:
- `PublicAPI.Shipped.txt`: Frozen public API contracts.
- `PublicAPI.Unshipped.txt`: Staged public APIs for the next version release.
- Build fails (`RS0016`/`RS0017`) if public members are added or modified without updating `PublicAPI.Unshipped.txt`.

---

## 3. Native AOT & Trimming Gates

Every push and pull request runs `EricksonLopez.Mediator.AotTest` with `PublishAot=true`:
- Zero `IL2026`, `IL2072`, `IL2091`, or `IL3050` trimming warnings allowed across all runtime packages (`TreatWarningsAsErrors=true`).
- Verifies that all commands, queries, pipelines, and notification handlers execute natively on bare metal without JIT compilation.

---

## 4. Code Coverage, SonarCloud & Mutation Gates

- **Unit Testing Pass Rate**: 100% pass rate required across all target frameworks (.NET 8.0, .NET 9.0, .NET 10.0).
- **Code Coverage**: Collected via `coverlet.collector` (OpenCover format) and uploaded to Codecov with per-PR quality status checks.
- **Static Code Analysis**: Integrated with SonarCloud via `dotnet-sonarscanner` in `dotnet-build-test.yml`.
- **Mutation Testing Gate (Stryker.NET)**:
  - **High**: $\ge 100\%$ — Excellent mutation resistance
  - **Low**: $\ge 98\%$ — Acceptable score
  - **Warning**: $\ge 95\%$ — Approaching break threshold
  - **Break**: $< 95\%$ — Hard gate failure (blocks release and commits status `failure`)
