# Repository Inventory — EricksonLopez.Mediator

This document provides a comprehensive inventory of all source projects, test suites, sample applications, dependencies, and build configurations in the `EricksonLopez.Mediator` repository.

---

## 1. Source Projects (9 Packages)

| Project | Target Framework(s) | Packable | Description |
|---|---|:---:|---|
| [`src/EricksonLopez.Mediator`](../src/EricksonLopez.Mediator) | `net8.0;net9.0;net10.0` | Yes | Core mediator contracts (`IMediator`, `ISender`, `IPublisher`, `ICommand`, `IQuery`, `INotification`, `IStreamRequest`), struct continuations (`INext<T>`, `INext`), attributes, and `StaticMediator`. |
| [`src/EricksonLopez.Mediator.Generator`](../src/EricksonLopez.Mediator.Generator) | `netstandard2.0` | Yes | Incremental Roslyn Source Generator weaving compile-time switch dispatch, handler registration, and IDE diagnostics (`ELM001`–`ELM011`). |
| [`src/EricksonLopez.Mediator.AspNetCore`](../src/EricksonLopez.Mediator.AspNetCore) | `net8.0;net9.0;net10.0` | Yes | ASP.NET Core Minimal API endpoint route extensions (`MapCommand`, `MapQuery`). |
| [`src/EricksonLopez.Mediator.OpenTelemetry`](../src/EricksonLopez.Mediator.OpenTelemetry) | `net8.0;net9.0;net10.0` | Yes | OpenTelemetry distributed tracing (`ActivitySource`) and runtime performance metrics (`Meter`) with zero reflection. |
| [`src/EricksonLopez.Mediator.Polly`](../src/EricksonLopez.Mediator.Polly) | `net8.0;net9.0;net10.0` | Yes | Polly v8 resilience pipeline behavior supporting retry, circuit breaker, and timeout policies. |
| [`src/EricksonLopez.Mediator.RateLimiting`](../src/EricksonLopez.Mediator.RateLimiting) | `net8.0;net9.0;net10.0` | Yes | Concurrency and rate limiting pipeline behavior using `System.Threading.RateLimiting`. |
| [`src/EricksonLopez.Mediator.Result`](../src/EricksonLopez.Mediator.Result) | `net8.0;net9.0;net10.0` | Yes | Decoupled result factory layer (`IResultFactory<TResponse>`) bridging mediator pipelines with `EricksonLopez.Result`. |
| [`src/EricksonLopez.Mediator.Testing`](../src/EricksonLopez.Mediator.Testing) | `net8.0;net9.0;net10.0` | Yes | Official in-memory `FakeMediator` and `DelegateNext` test doubles for unit testing without container setup. |
| [`src/EricksonLopez.Mediator.Validation`](../src/EricksonLopez.Mediator.Validation) | `net8.0;net9.0;net10.0` | Yes | Validation pipeline behavior integrating with FluentValidation and `EricksonLopez.Result.FluentValidation`. |

---

## 2. Test, Benchmark & Sample Projects

| Project | Target Framework(s) | Type | Purpose |
|---|---|:---:|---|
| [`tests/EricksonLopez.Mediator.Tests`](../tests/EricksonLopez.Mediator.Tests) | `net8.0;net9.0;net10.0` | Unit Tests | Unit tests for core attributes, exception classes, dispatch mechanics, lifetimes, Polly behaviors, and metrics. |
| [`tests/EricksonLopez.Mediator.Generator.Tests`](../tests/EricksonLopez.Mediator.Generator.Tests) | `net10.0` | Compiler Tests | Roslyn code generator compilation tests verifying dispatch emission, DI registration, and diagnostics (`ELM001`–`ELM011`). |
| [`tests/EricksonLopez.Mediator.IntegrationTests`](../tests/EricksonLopez.Mediator.IntegrationTests) | `net10.0` | Integration | End-to-end ASP.NET Core Minimal API integration tests via `WebApplicationFactory`. |
| [`tests/EricksonLopez.Mediator.AotTest`](../tests/EricksonLopez.Mediator.AotTest) | `net10.0` | Smoke Test | Native AOT (`PublishAot=true`) smoke test suite validating trimming safety and native execution. |
| [`tests/EricksonLopez.Mediator.Benchmarks`](../tests/EricksonLopez.Mediator.Benchmarks) | `net10.0` | Benchmarks | BenchmarkDotNet throughput and memory allocation performance suite. |
| [`samples/Sample`](../samples/Sample) | `net10.0` | Sample App | 14-level progressive executable showcase application demonstrating features from basic to advanced. |

---

## 3. Central Package Management (CPM)

Dependencies are pinned centrally in `Directory.Packages.props`:

| Package | Pinned Version | Scope |
|---|---|---|
| `Microsoft.Extensions.DependencyInjection.Abstractions` | `9.0.2` | Core & Integrations |
| `Microsoft.Extensions.DependencyInjection` | `9.0.2` | Core, Samples & Tests |
| `Microsoft.Extensions.Diagnostics.HealthChecks.Abstractions` | `9.0.2` | Core Health Checks |
| `System.Threading.RateLimiting` | `9.0.2` | Rate Limiting Extension |
| `OpenTelemetry.Api` | `1.17.0` | Observability Extension |
| `Polly.Core` | `8.5.2` | Resilience Extension |
| `FluentValidation` | `11.11.0` | Validation Extension |
| `EricksonLopez.Result` | `1.0.0` | Result Extension |
| `EricksonLopez.Result.FluentValidation` | `1.0.0` | Validation Extension |
| `Microsoft.CodeAnalysis.CSharp` | `4.8.0` | Roslyn Generator |
| `Microsoft.CodeAnalysis.Analyzers` | `3.3.4` | Roslyn Generator |
| `Microsoft.CodeAnalysis.PublicApiAnalyzers` | `3.3.4` | Public API Guardrails |
| `xunit` | `2.9.3` | Test Runner |
| `xunit.runner.visualstudio` | `3.0.2` | Test Runner |
| `AwesomeAssertions` | `9.5.0` | Test Assertions |
| `NSubstitute` | `5.3.0` | Mocking |
| `AutoFixture.Xunit2` | `4.18.1` | Test Data Fixtures |
| `coverlet.collector` | `6.0.4` | Code Coverage |
| `BenchmarkDotNet` | `0.15.8` | Performance Benchmarks |
| `Microsoft.AspNetCore.Mvc.Testing` | `9.0.2` | Integration Tests |
| `Microsoft.NET.Test.Sdk` | `17.14.1` | Test SDK |

---

## 4. Quality & Build Tooling

- **.NET SDK**: `10.0.x` (enables C# latest language features and multi-targeting down to .NET 8.0).
- **Static Analysis**: `TreatWarningsAsErrors=true`, `AnalysisLevel=latest-recommended`.
- **Trimming & AOT**: `EnableTrimAnalyzer=true`, `IsAotCompatible=true` across all runtime packages.
- **Mutation Testing**: Stryker.NET 4.16.0 (High: 100%, Low: 98%, Break: 95%).
- **Public API Tracking**: `PublicAPI.Shipped.txt` and `PublicAPI.Unshipped.txt` in core.
