# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [Unreleased]

---

## [1.0.0] - 2026-08-26

### Added
- **Core Interfaces & CQRS:** `IMediator`, `ISender`, `IPublisher`, `ICommand<T>`, `IQuery<T>`, `INotification`, `IStreamRequest<T>`, `ICommandHandler<T, R>`, `IQueryHandler<T, R>`, `INotificationHandler<T>`, `IStreamRequestHandler<T, R>`, and `StaticMediator`.
- **Zero-Allocation Struct Pipelines:** Compile-time monomorphized execution using `IPipelineBehavior<TRequest, TResponse>`, `INotificationBehavior<TNotification>`, and nested `struct` continuations (`INext<T>`, `INext`).
- **Multi-Targeting Ecosystem:** Full multi-targeting for `.NET 8.0` (LTS), `.NET 9.0` (STS), and `.NET 10.0` across all runtime and extension packages, with `netstandard2.0` support for the Roslyn Generator.
- **Roslyn Incremental Source Generator:** Compile-time dispatch tables, monomorphized pipelines, and DI registration (`services.AddEricksonLopezMediator()`) without runtime reflection or dynamic code emit.
- **Notification Strategies:** Sequential execution (default), parallel fan-out (`[PublishStrategy(PublishStrategy.Parallel)]`), and exception-aggregating sequential dispatch (`[PublishStrategy(PublishStrategy.SequentialAggregateExceptions)]`).
- **IDE Roslyn Diagnostics (ELM001–ELM011):** Real-time compiler diagnostics detecting missing handlers, duplicate handlers, invalid signatures, generic ambiguities, missing notifications, and behavior ordering conflicts.
- **ASP.NET Core Integration (`EricksonLopez.Mediator.AspNetCore`):** Native Minimal API endpoint mappings via `MapCommand` and `MapQuery`.
- **OpenTelemetry Observability (`EricksonLopez.Mediator.OpenTelemetry`):** Out-of-the-box distributed tracing with `ActivitySource` and execution metrics via `Meter`.
- **Polly Resilience Integration (`EricksonLopez.Mediator.Polly`):** Resilience pipelines (Retry, Circuit Breaker, Timeout, Hedging) powered by Polly v8 via `[UseResiliencePipeline]` and `AddMediatorPolly()`.
- **Rate Limiting (`EricksonLopez.Mediator.RateLimiting`):** Concurrency and throughput control with `System.Threading.RateLimiting`, emitting `RateLimitExceededException` with `RetryAfter` metadata.
- **Result Pattern Integration (`EricksonLopez.Mediator.Result`):** Dedicated AOT-safe short-circuiting integration package with `IResultFactory<TResponse>`.
- **FluentValidation Integration (`EricksonLopez.Mediator.FluentValidation`):** High-performance pipeline validation short-circuiting via `ValidationPipelineBehavior<TRequest, TResponse>` and `AddMediatorFluentValidation()`.
- **Health Checks (`EricksonLopez.Mediator`):** Built-in mediator readiness health checks via `AddMediatorHealthCheck()` and `MediatorHealthCheck`.
- **Testing Utilities (`EricksonLopez.Mediator.Testing`):** Test doubles including `FakeMediator`, assertion helpers (`ShouldHaveReceived<T>`, `ReceivedRequestsOf<T>`), and `DelegateNext` continuations.

### Breaking Changes
- **Result Pattern Decoupling (BC-001):** Removed `IResultFactory<TResponse>` and the `EricksonLopez.Result` dependency from the core `EricksonLopez.Mediator` package to ensure a zero-dependency core.
  - *Migration:* Projects using Result short-circuiting must install `EricksonLopez.Mediator.Result` and add `using EricksonLopez.Mediator.Result;`.
- **Package Replacement (BC-002):** Replaced `EricksonLopez.Mediator.Validation` with `EricksonLopez.Mediator.FluentValidation`.
  - *Migration:* In `.csproj`, replace `<PackageReference Include="EricksonLopez.Mediator.Validation" />` with `<PackageReference Include="EricksonLopez.Mediator.FluentValidation" />`.
- **Validation Pipeline Renaming (BC-003):** Renamed `ValidationBehavior<TRequest, TResponse>` to `ValidationPipelineBehavior<TRequest, TResponse>` and DI registration method `AddMediatorValidation()` to `AddMediatorFluentValidation()`.
  - *Migration:* Update DI configuration in `Program.cs` from `services.AddMediatorValidation()` to `services.AddMediatorFluentValidation()`.
- **Rate Limiting Exception Model (BC-004):** `RateLimitingBehavior` now throws `RateLimitExceededException` containing `RetryAfter` metadata instead of failing silently or throwing generic exceptions.
  - *Migration:* Exception handling middleware should handle `RateLimitExceededException` and map `RetryAfter` to HTTP 429 status codes.

### Removed
- Removed legacy package `EricksonLopez.Mediator.Validation` from solution and distribution channels.
- Removed transitive dependency on `EricksonLopez.Result` from core `EricksonLopez.Mediator`.

---

## [1.0.0-rc1] - 2026-08-13

### Added
- **Core Interfaces & CQRS**: Initial release candidate with `IMediator`, `ISender`, `IPublisher`, `ICommand<T>`, `IQuery<T>`, `INotification`, `ICommandHandler<T, R>`, `IQueryHandler<T, R>`, `INotificationHandler<T>`, and `StaticMediator`.
- **Zero-Allocation Pipelines**: Zero-allocation pipeline execution using `IPipelineBehavior<TRequest, TResponse>` and nested `struct` continuations (`INext<T>`, `INext`).
- **Behavior Ordering & Lifetimes**: Deterministic behavior ordering with `[UseGlobalBehavior]`, `[UseBehavior]`, and configurable DI lifetimes with `[ServiceLifetime]`.
- **Source Generator**: Roslyn Incremental Generator computing and weaving dispatch tables at compile-time.
- **IDE Roslyn Diagnostics**: Added compile-time diagnostics `ELM001` through `ELM011`.
- **ASP.NET Core Integration**: `EricksonLopez.Mediator.AspNetCore` package with Minimal API endpoint mappings.
- **OpenTelemetry Observability**: `EricksonLopez.Mediator.OpenTelemetry` package for automatic `ActivitySource` tracing.
- **Polly Resilience**: `EricksonLopez.Mediator.Polly` package with Polly v8 resilience policies.
- **Rate Limiting**: `EricksonLopez.Mediator.RateLimiting` package with `System.Threading.RateLimiting` pipeline behavior.
- **Testing Utilities**: `EricksonLopez.Mediator.Testing` package providing `FakeMediator` test double.
