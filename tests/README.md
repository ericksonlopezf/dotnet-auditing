# EricksonLopez.Mediator — Testing Guide & Architecture

## 1. Testing Architecture & 1:1 Project Symmetry

The test suite is architected with strict **1:1 Project Symmetry** between `src/` packages and dedicated `tests/` suites, segregating boundaries and ensuring full ecosystem dialect coverage:

| Source Package (`src/`) | Matching Test Project (`tests/`) | Dialect / Focus Area | Frameworks |
| :--- | :--- | :--- | :--- |
| `EricksonLopez.Mediator` | `EricksonLopez.Mediator.Tests` | Core Dispatching, Commands, Queries, Notifications, Streams, `INext` struct continuations | `net8.0;net9.0;net10.0` |
| `EricksonLopez.Mediator.AspNetCore` | `EricksonLopez.Mediator.AspNetCore.Tests` | Minimal API endpoint mapping (`MapCommand`, `MapQuery`), HTTP status codes, route builders | `net9.0;net10.0` |
| `EricksonLopez.Mediator.FluentValidation` | `EricksonLopez.Mediator.FluentValidation.Tests` | Input validation pipeline behaviors, `IValidator<T>` assembly registration, Result failure mapping | `net8.0;net9.0;net10.0` |
| `EricksonLopez.Mediator.Generator` | `EricksonLopez.Mediator.Generator.Tests` | Roslyn Incremental Generator, Code generation, Roslyn analyzers (`ELM001`–`ELM011`) | `net10.0` |
| `EricksonLopez.Mediator.OpenTelemetry` | `EricksonLopez.Mediator.OpenTelemetry.Tests` | Distributed tracing (`Activity`), metrics (`MediatorMetrics`), baggage propagation | `net8.0;net9.0;net10.0` |
| `EricksonLopez.Mediator.Polly` | `EricksonLopez.Mediator.Polly.Tests` | Polly v8 resilience pipelines, `[UseResiliencePipeline]`, Retry, Circuit Breaker, Timeout | `net8.0;net9.0;net10.0` |
| `EricksonLopez.Mediator.RateLimiting` | `EricksonLopez.Mediator.RateLimiting.Tests` | Rate limiting pipelines, Concurrency limiter, Partitioned limiter, `RateLimitExceededException` | `net8.0;net9.0;net10.0` |
| `EricksonLopez.Mediator.Result` | `EricksonLopez.Mediator.Result.Tests` | Result Pattern dialect, `IResultFactory<TResponse>`, Error code mapping | `net8.0;net9.0;net10.0` |
| `EricksonLopez.Mediator.Testing` | `EricksonLopez.Mediator.Testing.Tests` | Test doubles, `FakeMediator`, `DelegateNext<TResponse>`, `DelegateNext`, Spies & assertions | `net8.0;net9.0;net10.0` |

### Supporting Test Suites
| Supporting Project | Type | Purpose |
| :--- | :--- | :--- |
| `EricksonLopez.Mediator.IntegrationTests` | End-to-End Integration | Full-stack Minimal API server integration with all behaviors combined |
| `EricksonLopez.Mediator.AotSmokeTest` | Native AOT Verification | AOT trimming and runtime verification |
| `EricksonLopez.Mediator.Benchmarks` | Performance Benchmarks | Allocation and throughput benchmarks vs MediatR and direct dispatch |

---

## 2. Ecosystem Dialects Coverage Matrix

All messaging and integration dialects supported by the mediator are thoroughly covered:

1. **Command Dialect (`ICommand<TResponse>`, `ICommand`)**:
   - Tested via `EricksonLopez.Mediator.Tests` and `EricksonLopez.Mediator.AspNetCore.Tests`.
   - Asynchronous execution, cancellation tokens, unit commands (`ICommand<Unit>` / `ICommand`).
2. **Query Dialect (`IQuery<TResponse>`)**:
   - Tested via `EricksonLopez.Mediator.Tests` and `EricksonLopez.Mediator.AspNetCore.Tests`.
   - Side-effect-free semantics, strongly-typed return values.
3. **Notification Dialect (`INotification`)**:
   - Tested via `EricksonLopez.Mediator.Tests`.
   - Multi-handler pub-sub execution strategies: `Sequential`, `Parallel`, `SequentialAggregateExceptions`.
4. **Streaming Dialect (`IStreamRequest<TResponse>`)**:
   - Tested via `EricksonLopez.Mediator.Tests` and `EricksonLopez.Mediator.Testing.Tests`.
   - Zero-allocation `IAsyncEnumerable<TResponse>` streaming with cooperative cancellation.
5. **Result Pattern Dialect (`Result<T>`, `IResultFactory<TResponse>`)**:
   - Tested via `EricksonLopez.Mediator.Result.Tests` and `EricksonLopez.Mediator.FluentValidation.Tests`.
   - Error code propagation, `Error` factory mapping without throwing exceptions.
6. **FluentValidation Dialect (`ValidationPipelineBehavior`)**:
   - Tested via `EricksonLopez.Mediator.FluentValidation.Tests`.
   - Validator resolution, short-circuiting on failure, validation exception vs Result failure.
7. **Polly Resilience Dialect (`PollyResilienceBehavior`)**:
   - Tested via `EricksonLopez.Mediator.Polly.Tests`.
   - Retry, exponential backoff, circuit breaker state transitions, timeout rejection, keyed pipelines.
8. **OpenTelemetry Observability Dialect (`OpenTelemetryBehavior`, `MediatorMetrics`)**:
   - Tested via `EricksonLopez.Mediator.OpenTelemetry.Tests`.
   - Activity span creation, tags, status tracking, duration and counter metrics recording.
9. **Rate Limiting Dialect (`RateLimitingBehavior`)**:
   - Tested via `EricksonLopez.Mediator.RateLimiting.Tests`.
   - Token bucket, concurrency limiting, partitioned rate limiting per tenant/key, `RetryAfter` metadata.
10. **Testing Doubles Dialect (`FakeMediator`, `DelegateNext`)**:
    - Tested via `EricksonLopez.Mediator.Testing.Tests`.
    - Spying, fluent assertions (`ShouldHaveReceived`, `ShouldNotHaveReceived`), reset mechanics, zero-allocation struct continuations.

---

## 3. Conventions and Best Practices

1. **Osherove Naming Pattern (ADR-031 — Living Specifications)**:
   - All test methods strictly follow the pattern **`[Method]_[Scenario]_[Result]`** or **`[UnitOfWork]_[StateUnderTest]_[ExpectedBehavior]`**.
   - Test names serve as human-readable living specifications in runners and CI/CD reporting.
2. **State Isolation and Immutability**:
   - Mutable static state in test handlers (`public static bool Invoked`) is forbidden.
   - State trackers (`TestStateTracker`, `NotificationAuditLog`) are registered in the DI container per test scope.
3. **Modularization of Test Fixtures**:
   - Test contracts and handlers are organized into single-responsibility submodules within test fixtures.
4. **Uniform Assertions**:
   - `AwesomeAssertions` is used consistently across all test projects (`result.Should().Be(...)`).
5. **Reusable Pipeline Continuations**:
   - `DelegateNext<TResponse>` and `DelegateNext` (provided by `EricksonLopez.Mediator.Testing`) are used for struct-based zero-allocation pipeline testing.
6. **Multi-Targeting**:
   - Test projects execute on `net8.0`, `net9.0`, and `net10.0` to guarantee cross-framework runtime parity.

---

## 4. Execution Commands

### Unit and Integration Tests
```pwsh
# Run entire test suite
dotnet test

# Run with normal console output
dotnet test --logger:"console;verbosity=normal"

# Run a specific project
dotnet test tests/EricksonLopez.Mediator.Tests/EricksonLopez.Mediator.Tests.csproj
dotnet test tests/EricksonLopez.Mediator.Polly.Tests/EricksonLopez.Mediator.Polly.Tests.csproj
dotnet test tests/EricksonLopez.Mediator.OpenTelemetry.Tests/EricksonLopez.Mediator.OpenTelemetry.Tests.csproj

# Run on a specific target framework
dotnet test --framework net8.0
dotnet test --framework net9.0
dotnet test --framework net10.0
```

### Code Coverage Execution
```pwsh
dotnet test --collect:"XPlat Code Coverage"
```

### Stryker.NET Mutation Testing
```pwsh
# Core Suite
dotnet stryker --config-file stryker-config.json

# Individual Package Suites
dotnet stryker --config-file stryker-generator-config.json
dotnet stryker --config-file stryker-polly-config.json
dotnet stryker --config-file stryker-opentelemetry-config.json
dotnet stryker --config-file stryker-ratelimiting-config.json
dotnet stryker --config-file stryker-fluentvalidation-config.json
dotnet stryker --config-file stryker-result-config.json
dotnet stryker --config-file stryker-testing-config.json
dotnet stryker --config-file stryker-aspnetcore-config.json
```
