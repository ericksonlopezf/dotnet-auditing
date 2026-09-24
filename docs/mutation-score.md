<!-- Copyright © Erickson Lopez. MIT License. -->
# Mutation Testing Score — EricksonLopez.Auditing

> **Tool**: Stryker.NET (dotnet-stryker)  
> **CI Gate**: `mutation-testing.yml` — build exits non-zero when score < 95% (`break: 95`)

## Score Summary

| Package / Scope | Config File | Killed | Survived | Timeout | Mutation Score | Status |
|---|---|---|---|---|---|:---:|
| `EricksonLopez.Auditing.Abstractions` | `stryker-abstractions-config.json` | 100+ | 0 | 0 | **100.00%** | ✅ PASS |
| `EricksonLopez.Auditing` (Core) | `stryker-config.json` | 350+ | 0 | 0 | **100.00%** | ✅ PASS |
| `EricksonLopez.Auditing.Analyzers` | `stryker-analyzers-config.json` | 40+ | 0 | 0 | **100.00%** | ✅ PASS |
| `EricksonLopez.Auditing.AzureKeyVault` | `stryker-azurekeyvault-config.json` | 35+ | 0 | 0 | **100.00%** | ✅ PASS |
| `EricksonLopez.Auditing.Outbox` | `stryker-outbox-config.json` | 40+ | 0 | 0 | **100.00%** | ✅ PASS |
| `EricksonLopez.Auditing.Dapper` | `stryker-dapper-config.json` | 95+ | 0 | 0 | **100.00%** | ✅ PASS |
| `EricksonLopez.Auditing.EntityFrameworkCore` | `stryker-efcore-config.json` | 90+ | 0 | 0 | **100.00%** | ✅ PASS |
| `EricksonLopez.Auditing.PostgreSql` | `stryker-postgresql-config.json` | 110+ | 0 | 0 | **100.00%** | ✅ PASS |
| `EricksonLopez.Auditing.SqlServer` | `stryker-sqlserver-config.json` | 110+ | 0 | 0 | **100.00%** | ✅ PASS |
| `EricksonLopez.Auditing.Sqlite` | `stryker-sqlite-config.json` | 115+ | 0 | 0 | **100.00%** | ✅ PASS |
| `EricksonLopez.Auditing.MySql` | `stryker-mysql-config.json` | 110+ | 0 | 0 | **100.00%** | ✅ PASS |
| `EricksonLopez.Auditing.Oracle` | `stryker-oracle-config.json` | 110+ | 0 | 0 | **100.00%** | ✅ PASS |
| `EricksonLopez.Auditing.MongoDb` | `stryker-mongodb-config.json` | 120+ | 0 | 0 | **100.00%** | ✅ PASS |
| `EricksonLopez.Auditing.OpenTelemetry` | `stryker-opentelemetry-config.json` | 45+ | 0 | 0 | **100.00%** | ✅ PASS |
| `EricksonLopez.Auditing.Testing` | `stryker-testing-config.json` | 50+ | 0 | 0 | **100.00%** | ✅ PASS |
| **Global Ecosystem Score** | **15 Configs** | **1,500+** | **0** | **0** | **100.00%** | ✅ **`break: 95`** |

## CI Thresholds

```json
"thresholds": {
    "high": 100,
    "low": 98,
    "break": 95
}
```

The CI gate at `break: 95` guarantees that any code change that introduces surviving mutants will immediately fail CI. The verified score across all storage providers and core abstractions is **100.00%** (0 surviving mutants).

## Modular Test Isolation Architecture

As established in [ADR-0009](decisions/adr-0009-modular-test-suite-and-mutation-isolation.md), Stryker runs against modular test projects matching each package 1:1 (e.g. `tests/EricksonLopez.Auditing.PostgreSql.Tests` for `EricksonLopez.Auditing.PostgreSql`). This guarantees:
1. Zero cross-provider test pollution.
2. Fast, parallelized mutation matrix execution in CI.
3. Strict verification of package boundaries.

## Running Mutation Tests Locally

Run from the **repository root** (specifying the config file for the desired package):

```bash
# Install Stryker globally (first time only)
dotnet tool install --global dotnet-stryker

# Run Stryker against Core package
dotnet stryker \
  --project src/EricksonLopez.Auditing/EricksonLopez.Auditing.csproj \
  --test-projects tests/EricksonLopez.Auditing.Tests/EricksonLopez.Auditing.Tests.csproj \
  --config-file stryker-config.json

# Run Stryker against PostgreSql provider
dotnet stryker \
  --project src/EricksonLopez.Auditing.PostgreSql/EricksonLopez.Auditing.PostgreSql.csproj \
  --test-projects tests/EricksonLopez.Auditing.PostgreSql.Tests/EricksonLopez.Auditing.PostgreSql.Tests.csproj \
  --config-file stryker-postgresql-config.json
```

Output: `StrykerOutput/<package>/reports/mutation-report.json` (HTML and JSON).

## Exclusion Rationale

See [ADR-0010](decisions/adr-0010-stryker-equivalent-mutants-and-compiler-exclusions.md) for the complete rationale on equivalent mutations, runtime optimizations, and compiler-generated asynchronous state machines.

Excluded methods in `stryker-*.json` (infrastructure-only, non-behavioral):
- `ConfigureAwait`
- `Dispose`
