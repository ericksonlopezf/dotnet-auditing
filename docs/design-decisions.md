# Architecture Decision Records (ADR) Index — EricksonLopez.Mediator

This document indexes all 35 Architecture Decision Records (ADRs) and architectural invariants governing `EricksonLopez.Mediator`.

Authoritative markdown records are located in [`docs/adr/`](adr/).

---

## ADR Registry (1 to 35)

| ADR | Title | Status | Scope |
|---|---|---|---|
| [ADR-001](adr/001-compile-time-dispatch-via-source-generator.md) | Compile-Time Dispatch via Source Generator | Approved | Core / Generator |
| [ADR-002](adr/002-zero-allocation-pipeline-via-struct-inext-tresponse.md) | Zero-Allocation Pipeline via Struct `INext<TResponse>` | Approved | Core / Pipeline |
| [ADR-003](adr/003-icommand-t-and-iquery-t-separation-cqrs-semantic-enforcement.md) | `ICommand<T>` and `IQuery<T>` Separation for CQRS Semantics | Approved | Core API |
| [ADR-004](adr/004-valuetask-as-the-canonical-return-type.md) | `ValueTask` as the Canonical Return Type | Approved | Core API |
| [ADR-005](adr/005-iresultfactory-tresponse-aot-safe-pipeline-short-circuit.md) | `IResultFactory<TResponse>` AOT-Safe Pipeline Short-Circuit | Approved | Result Integration |
| [ADR-006](adr/006-no-runtime-di-scanning.md) | No Runtime DI Scanning | Approved | DI / AOT |
| [ADR-007](adr/007-attribute-based-behavior-configuration.md) | Attribute-Based Pipeline Behavior Configuration | Approved | Pipeline |
| [ADR-008](adr/008-sequential-notification-execution-as-default.md) | Sequential Notification Execution as Default | Approved | Notifications |
| [ADR-009](adr/009-imediator-as-singleton.md) | `IMediator` Registered as Singleton | Approved | DI |
| [ADR-010](adr/010-rejected-runtime-reflection-for-dispatch.md) | Rejected: Runtime Reflection for Dispatch | Rejected | Architecture |
| [ADR-011](adr/011-rejected-assembly-scanning-for-handler-discovery.md) | Rejected: Assembly Scanning at Runtime | Rejected | Architecture |
| [ADR-012](adr/012-deferred-multi-assembly-handler-discovery.md) | Multi-Assembly Handler Discovery via `[DiscoverHandlers]` | Approved | Generator |
| [ADR-013](adr/013-rejected-requesthandlerdelegate-t-func-based-pipeline.md) | Rejected: Delegate-Based Pipeline Closures | Rejected | Pipeline |
| [ADR-014](adr/014-rejected-built-in-auto-retry.md) | Rejected: Built-in Automatic Retry Policy | Rejected | Scope |
| [ADR-015](adr/015-rejected-v1-0-iasyncenumerable-t-streaming.md) | ~~Experimental `IAsyncEnumerable<T>` Streaming~~ **Superseded by ADR-034** | ~~Approved (Exp.)~~ Superseded | Streaming |
| [ADR-016](adr/016-rejected-built-in-authorization.md) | Rejected: Built-in Authorization Engine | Rejected | Scope |
| [ADR-017](adr/017-rejected-built-in-caching.md) | Rejected: Built-in Caching Engine | Rejected | Scope |
| [ADR-018](adr/018-rejected-built-in-transactions-unit-of-work.md) | Rejected: Built-in Unit of Work & Transactions | Rejected | Scope |
| [ADR-019](adr/019-rejected-outbox-inbox-saga-scheduling.md) | Rejected: Outbox, Inbox, Sagas, Scheduling in Core | Rejected | Scope |
| [ADR-020](adr/020-rejected-convention-based-handler-discovery-no-interfaces.md) | Rejected: Convention-Based Discovery without Interfaces | Rejected | API Design |
| [ADR-021](adr/021-rejected-service-locator-pattern.md) | Rejected: Service Locator Pattern | Rejected | DI |
| [ADR-022](adr/022-segregated-isender-ipublisher-interfaces.md) | Segregated `ISender` and `IPublisher` Interfaces | Approved | Core API |
| [ADR-023](adr/023-deferred-opentelemetry-in-core.md) | OpenTelemetry in Dedicated Extension Package | Approved | Observability |
| [ADR-024](adr/024-multi-target-net8-net9-net10.md) | Multi-Targeting .NET 8.0, 9.0, and 10.0 | Approved | Compatibility |
| [ADR-025](adr/025-unified-notification-behavior-struct-inext.md) | Struct-Based Zero-Allocation Notification Behaviors | Approved | Notifications |
| [ADR-026](adr/026-decoupling-result-pattern-into-mediator-result.md) | Decoupling Result Pattern into `EricksonLopez.Mediator.Result` | Approved | Result Integration |
| [ADR-027](adr/027-configurable-service-lifetime-for-behaviors.md) | Configurable `[ServiceLifetime]` on Pipeline Behaviors | Approved | DI |
| [ADR-028](adr/028-compile-time-generic-behavior-constraint-validation.md) | Compile-Time Generic Behavior Constraint Validation | Approved | Generator |
| [ADR-029](adr/029-exception-aggregation-via-publish-strategy.md) | Exception Aggregation via `PublishStrategy.SequentialAggregateExceptions` | Approved | Notifications |
| [ADR-030](adr/030-rejection-of-duplicate-in-process-cqrs-dispatchers.md) | Rejection of Duplicate In-Process CQRS Dispatchers | Approved | Architecture |
| [ADR-031](adr/031-institutional-testing-osherove-naming-pattern.md) | Institutional Testing: Osherove Naming Pattern & IDE1006 Suppression | Approved | Testing |
| [ADR-032](adr/032-deprecation-staticmediator-polymorphic-send-overloads.md) | Deprecation of Polymorphic Reflection Overloads in `StaticMediator` | Approved | Core API |
| [ADR-033](adr/033-deprecation-validation-package-in-favor-of-fluentvalidation.md) | Deprecation of `EricksonLopez.Mediator.Validation` in favor of FluentValidation | Approved | Packages |
| [ADR-034](adr/034-promotion-streaming-stable-direct-handler-dispatch.md) | Promotion of Streaming to Stable via Direct Handler Dispatch | Approved | Streaming |
| [ADR-035](adr/035-institutional-testing-xunit-assert-in-generator-tests.md) | Institutional Testing: xUnit Assert in Generator Tests Exception | Approved | Testing |
| [REJECT-002](adr/reject-002-state-machine-in-mediator.md) | Rejection of Stateful Workflow and Saga Management inside Mediator | Invariant | Architecture |
