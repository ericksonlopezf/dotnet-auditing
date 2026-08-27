# Packages Overview — EricksonLopez.Mediator

`EricksonLopez.Mediator` is engineered as a modular suite of specialized packages. Each package adheres strictly to Single Responsibility and zero-allocation runtime paths. Native AOT compatibility varies by package — see individual entries for details.


---

## 1. `EricksonLopez.Mediator` (Core)
The foundational library providing core CQRS interfaces (`ISender`, `IPublisher`, `IMediator`, `ICommand<T>`, `IQuery<T>`, `INotification`, `IStreamRequest<T>`), handler contracts (`ICommandHandler`, `IQueryHandler`, `INotificationHandler`, `IStreamRequestHandler`), pipeline behavior contracts (`IPipelineBehavior`, `INotificationBehavior`), struct continuation tokens (`INext<T>`, `INext`), attributes, and the `StaticMediator` high-performance direct dispatcher.

- **TargetFrameworks**: `net8.0`, `net9.0`, `net10.0`
- **Dependencies**: `Microsoft.Extensions.DependencyInjection.Abstractions`, `Microsoft.Extensions.Diagnostics.HealthChecks.Abstractions`
- **AOT Readiness**: 100% Native AOT compatible (0 trim warnings)

```bash
dotnet add package EricksonLopez.Mediator
```

---

## 2. `EricksonLopez.Mediator.Generator` (Source Generator)
Roslyn Incremental Source Generator and Analyzer. Analyzes your project syntax trees at compile time to construct monomorphized switch dispatchers, generate `AddEricksonLopezMediator()` DI registration methods, and enforce architectural invariants via compiler diagnostics (`ELM001` through `ELM011`).

- **TargetFramework**: `netstandard2.0` (Roslyn analyzer component)
- **Dependencies**: `Microsoft.CodeAnalysis.CSharp` (PrivateAssets=all)
- **AOT Readiness**: N/A (Build-time analyzer)

```xml
<PackageReference Include="EricksonLopez.Mediator.Generator" Version="1.0.0" OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
```

---

## 3. `EricksonLopez.Mediator.AspNetCore` (Minimal APIs)
Provides strongly-typed endpoint route mapping extensions for ASP.NET Core Minimal APIs (`MapCommand`, `MapQuery`), connecting HTTP endpoints directly to mediator dispatch without controller boilerplate.

- **TargetFrameworks**: `net8.0`, `net9.0`, `net10.0`
- **Dependencies**: `EricksonLopez.Mediator`, `Microsoft.AspNetCore.App` (FrameworkReference)
- **AOT Readiness**: ⚠️ Requires additional AOT configuration. `MapCommand` and `MapQuery` use ASP.NET Core Minimal API route delegate binding, which is annotated with `[RequiresUnreferencedCode]`. Additional AOT source-generation configuration on request types may be required when publishing with `PublishAot=true`.

```bash
dotnet add package EricksonLopez.Mediator.AspNetCore
```

---

## 4. `EricksonLopez.Mediator.OpenTelemetry` (Observability)
Zero-overhead distributed tracing and metrics integration. Emits OpenTelemetry `Activity` spans and `Meter` instruments with pre-cached type metadata, eliminating runtime reflection in telemetry hot paths.

- **TargetFrameworks**: `net8.0`, `net9.0`, `net10.0`
- **Dependencies**: `EricksonLopez.Mediator`, `OpenTelemetry.Api`, `Microsoft.Extensions.DependencyInjection.Abstractions`
- **AOT Readiness**: 100% Native AOT compatible

```bash
dotnet add package EricksonLopez.Mediator.OpenTelemetry
```

---

## 5. `EricksonLopez.Mediator.Polly` (Resilience)
Polly v8 resilience pipeline integration. Provides `PollyResilienceBehavior` and `[UseResiliencePipeline]` attributes to wrap command and query execution with retry, circuit breaker, rate limiter, and timeout strategies.

- **TargetFrameworks**: `net8.0`, `net9.0`, `net10.0`
- **Dependencies**: `EricksonLopez.Mediator`, `Polly.Core`, `Microsoft.Extensions.DependencyInjection.Abstractions`
- **AOT Readiness**: ⚠️ Generally AOT-compatible. The `[UseResiliencePipeline]` attribute is read via `GetCustomAttribute<T>()` in a closed-generic static initializer (per ADR-030). Under aggressive trimming the attribute metadata must be explicitly preserved. Explicit configuration without assembly scanning is recommended for AOT workloads.

```bash
dotnet add package EricksonLopez.Mediator.Polly
```

---

## 6. `EricksonLopez.Mediator.RateLimiting` (Rate Limiting)
High-throughput in-process rate limiting pipeline behavior built directly on `System.Threading.RateLimiting`. Protects handlers against burst traffic and resource starvation.

- **TargetFrameworks**: `net8.0`, `net9.0`, `net10.0`
- **Dependencies**: `EricksonLopez.Mediator`, `System.Threading.RateLimiting`, `Microsoft.Extensions.DependencyInjection.Abstractions`
- **AOT Readiness**: 100% Native AOT compatible

```bash
dotnet add package EricksonLopez.Mediator.RateLimiting
```

---

## 7. `EricksonLopez.Mediator.Result` (Result Pattern Integration)
Provides `IResultFactory<TResponse>` bridging the mediator pipeline with `EricksonLopez.Result` for exception-free validation, failure propagation, and pipeline short-circuiting.

- **TargetFrameworks**: `net8.0`, `net9.0`, `net10.0`
- **Dependencies**: `EricksonLopez.Mediator`, `EricksonLopez.Result`
- **AOT Readiness**: 100% Native AOT compatible

```bash
dotnet add package EricksonLopez.Mediator.Result
```

---

## 8. `EricksonLopez.Mediator.Testing` (Unit Testing Double)
Provides the official `FakeMediator` test double and `DelegateNext` continuations. Allows unit tests to verify request dispatching, setup stub responses, and assert notifications without spinning up an `IServiceProvider`.

- **TargetFrameworks**: `net8.0`, `net9.0`, `net10.0`
- **Dependencies**: `EricksonLopez.Mediator`
- **AOT Readiness**: Test-only library

```bash
dotnet add package EricksonLopez.Mediator.Testing
```

---

## 9. `EricksonLopez.Mediator.Validation` (Validation Pipeline — DEPRECATED)

> [!WARNING]
> **This package is deprecated (ADR-033) and will be archived in v2.0.**
> Migrate to [`EricksonLopez.Mediator.FluentValidation`](#10-ericksonlopezmediat0rfluent-validation-fluent-validation-pipeline) which provides the same functionality via `ValidationPipelineBehavior<TRequest, TResponse>` and `AddMediatorFluentValidation()`.

Validation pipeline behavior integrating FluentValidation rules with `EricksonLopez.Mediator` and `EricksonLopez.Result.FluentValidation` for structured error reporting.

- **TargetFrameworks**: `net8.0`, `net9.0`, `net10.0`
- **Dependencies**: `EricksonLopez.Mediator`, `EricksonLopez.Mediator.Result`, `EricksonLopez.Result.FluentValidation`, `Microsoft.Extensions.DependencyInjection.Abstractions`
- **AOT Readiness**: ❌ Not AOT compatible. `AddMediatorValidatorsFromAssembly` uses `AssemblyScanner` which relies on `[RequiresUnreferencedCode]` assembly scanning.

```bash
dotnet add package EricksonLopez.Mediator.Validation
```

---

## 10. `EricksonLopez.Mediator.FluentValidation` (FluentValidation Pipeline)
The recommended FluentValidation integration. Provides `ValidationPipelineBehavior<TRequest, TResponse>` and fluent DI extensions (`AddMediatorFluentValidation()`, `AddMediatorFluentValidationValidator<TValidator, TRequest>()`, `AddMediatorFluentValidatorsFromAssembly()`). Replaces the deprecated `EricksonLopez.Mediator.Validation` package (ADR-033).

- **TargetFrameworks**: `net8.0`, `net9.0`, `net10.0`
- **Dependencies**: `EricksonLopez.Mediator`, `EricksonLopez.Mediator.Result`, `EricksonLopez.Result.FluentValidation`, `Microsoft.Extensions.DependencyInjection.Abstractions`
- **AOT Readiness**: ⚠️ `ValidationPipelineBehavior<TRequest, TResponse>` itself is AOT-safe when validators are registered explicitly via `AddMediatorFluentValidationValidator<TValidator, TRequest>()`. Assembly scanning (`AddMediatorFluentValidatorsFromAssembly`) uses `[RequiresUnreferencedCode]` and is not AOT-compatible.

```bash
dotnet add package EricksonLopez.Mediator.FluentValidation
```
