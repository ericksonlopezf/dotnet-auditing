# ROADMAP.md — EricksonLopez.Mediator

## MVP — v1.0 (COMPLETED)

Version `1.0.0-rc1` has been released. It delivers the fastest in-process, AOT-compatible, reflection-free dispatcher for .NET.

### Features Delivered in v1.0:
- [x] Core API (IMediator, ISender, IPublisher, ICommand, IQuery, INotification)
- [x] Zero-allocation pipeline execution (`IPipelineBehavior`, `INext`)
- [x] Behavior ordering via `[UseGlobalBehavior]` and `[UseBehavior]`
- [x] Parallel notification execution
- [x] Roslyn Source Generator for DI and dispatch
- [x] Testing utilities (`FakeMediator`, `MockBuilder`)
- [x] Native OpenTelemetry observability

*See `CHANGELOG.md` for a full list of implemented features.*

---

## ADVANCED CAPABILITIES & PACKAGES (COMPLETED)

All architectural enhancements and ecosystem integration packages are fully implemented and verified.

### Architecture & Capabilities
- [x] Streaming `IAsyncEnumerable<T>` support
- [x] Static handler mode (no DI) for maximum performance in constrained environments (`StaticMediator`)
- [x] Generic static dispatch as an alternative to switch statements (`SendCommand`, `SendQuery`)
- [x] Notification pipeline behaviors (`INotificationBehavior<T>`)
- [x] Source-generated validation via attributes (`[ValidateRequest]`, `[ValidateNotNull]`, `[ValidateNotEmpty]`, `[ValidateRange]`, `[ValidateLength]`, `[ValidateRegex]`)
- [x] Cross-assembly handler composition and discovery (`[DiscoverHandlers]`)

### Tooling & Ecosystem Packages
- [x] `EricksonLopez.Mediator` — Core CQRS abstractions, struct `INext`, and `StaticMediator`
- [x] `EricksonLopez.Mediator.Generator` — Roslyn Source Generator for monomorphized dispatch
- [x] `EricksonLopez.Mediator.AspNetCore` — Minimal API endpoint mapping extensions
- [x] `EricksonLopez.Mediator.OpenTelemetry` — Distributed tracing and metrics
- [x] `EricksonLopez.Mediator.Polly` — Polly v8 resilience policies behavior
- [x] `EricksonLopez.Mediator.RateLimiting` — System.Threading.RateLimiting behavior
- [x] `EricksonLopez.Mediator.Result` — Result pattern short-circuiting with `IResultFactory<T>`
- [x] `EricksonLopez.Mediator.Testing` — In-memory `FakeMediator` and `DelegateNext` test doubles
- [x] `EricksonLopez.Mediator.Validation` — FluentValidation pipeline integration

---

## COMPATIBILITY POLICY

Target frameworks: `net8.0`, `net9.0`, `net10.0` (core), `netstandard2.0` (generator)
AOT & Trimming: Fully supported and required to remain supported in all future versions.
SemVer: Strict Semantic Versioning. No breaking changes in `1.x` releases.
