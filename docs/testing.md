# Testing Strategy & Automated Quality Verification

## 1. Multi-Tiered Testing Strategy

1. **Roslyn Generator Snapshot & Semantic Tests** (`EricksonLopez.Mediator.Generator.Tests`): Automated tests validating syntax trees, dispatch tables, and compiler diagnostic emission.
2. **Behavior Middleware Tests** (`EricksonLopez.Mediator.FluentValidation.Tests`, `EricksonLopez.Mediator.Polly.Tests`, `EricksonLopez.Mediator.OpenTelemetry.Tests`, `EricksonLopez.Mediator.RateLimiting.Tests`, `EricksonLopez.Mediator.Result.Tests`): Full test coverage of middleware pipelines.
3. **ASP.NET Core Integration Tests** (`EricksonLopez.Mediator.AspNetCore.Tests`): Validates Minimal API endpoint mapping.
4. **Zero-Allocation Memory Assertions** (`EricksonLopez.Mediator.Tests`): Enforces 0 bytes GC heap allocation across pipelines.
5. **Native AOT Smoke Tests** (`EricksonLopez.Mediator.AotSmokeTest`): Validates single-file AOT executable compilation and runtime execution.
