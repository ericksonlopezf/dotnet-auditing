# Public API Surface Budget & Binary Footprint

## 1. Budget Limits & Allocation Metrics

| Assembly / Package | Max Binary Size (Packaged DLL) | Max Types in Public API | Max GC Allocation per Dispatch |
|---|---|---|---|
| `EricksonLopez.Mediator` | **< 60 KB** | **<= 25 Types** | **0 B** |
| `EricksonLopez.Mediator.Generator` | **< 300 KB** | **<= 5 Internal Types** | **N/A (Compile-time)** |
| `EricksonLopez.Mediator.FluentValidation` | **< 40 KB** | **<= 8 Types** | **0 B (Success path)** |
| `EricksonLopez.Mediator.Polly` | **< 45 KB** | **<= 6 Types** | **0 B** |
| `EricksonLopez.Mediator.OpenTelemetry` | **< 45 KB** | **<= 6 Types** | **0 B** |
| `EricksonLopez.Mediator.RateLimiting` | **< 40 KB** | **<= 6 Types** | **0 B** |
| `EricksonLopez.Mediator.Result` | **< 30 KB** | **<= 6 Types** | **0 B** |
| `EricksonLopez.Mediator.AspNetCore` | **< 35 KB** | **<= 5 Types** | **0 B** |
| `EricksonLopez.Mediator.Testing` | **< 35 KB** | **<= 6 Types** | **0 B** |

---

## 2. Dependency Invariance
- `EricksonLopez.Mediator` has **0 external NuGet dependencies** (depends only on core BCL).
- Middleware packages depend solely on their dedicated official abstractions (`Polly.Core`, `OpenTelemetry.Api`, `FluentValidation`).
