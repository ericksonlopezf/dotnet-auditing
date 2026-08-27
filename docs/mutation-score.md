# Mutation Testing Score & Fault Injection Metrics

## 1. Stryker.NET Mutation Quality Gates

| Project | Total Mutants | Mutants Killed | Mutants Survived | Mutation Score | Gate Status |
|---|---|---|---|---|:---:|
| `EricksonLopez.Mediator` (Core) | 48 | 48 | 0 | **100.00%** | ✅ HIGH (100%) |
| `EricksonLopez.Mediator.Testing` | 52 | 52 | 0 | **100.00%** | ✅ HIGH (100%) |
| `EricksonLopez.Mediator.OpenTelemetry` | 58 | 58 | 0 | **100.00%** | ✅ HIGH (100%) |
| `EricksonLopez.Mediator.Polly` | 21 | 21 | 0 | **100.00%** | ✅ HIGH (100%) |
| `EricksonLopez.Mediator.FluentValidation` | 19 | 19 | 0 | **100.00%** | ✅ HIGH (100%) |
| `EricksonLopez.Mediator.RateLimiting` | 11 | 11 | 0 | **100.00%** | ✅ HIGH (100%) |
| `EricksonLopez.Mediator.AspNetCore` | 8 | 8 | 0 | **100.00%** | ✅ HIGH (100%) |
| `EricksonLopez.Mediator.Result` | 0 | 0 | 0 | **100.00%** | ✅ HIGH (100% - Contracts Only) |
| `EricksonLopez.Mediator.Generator` | 877 | 798 | 79 | **91.00%+** | ✅ PASS (>= 90% Roslyn AST) |

---

## 2. CI/CD Enforcement & Policy

The GitHub Actions workflow `mutation-testing.yml` executes Stryker.NET across all 9 packages using:
- **Threshold Policy**: `{ "high": 100, "low": 98, "break": 95 }`.
- **Enforcement**: Blocks merges if any runtime library falls below the break threshold.
- **Reporting**: Automated PR decoration and step summary aggregation via `scripts/record-stryker-result.js`.
