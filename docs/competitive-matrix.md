# Competitive Feature Matrix — EricksonLopez.Mediator

A comprehensive evaluation of `EricksonLopez.Mediator` against the most prominent in-process messaging solutions for .NET.

---

## Master Evaluation Matrix

| Domain | Feature | EricksonLopez.Mediator | MediatR | Martinothamar/Mediator |
|---|---|---|---|---|
| **Dispatch** | Compile-Time Code Generation | ✅ (Roslyn Source Generator) | ❌ (Runtime reflection) | ✅ (Source Generator) |
| **Dispatch** | Native AOT Trimming Safety | ✅ (0 Trimming Warnings) | ⚠️ (Reflection warnings) | ⚠️ (Partial) |
| **Dispatch** | Direct Switch Pattern Matching | ✅ | ❌ | ✅ |
| **Pipelines** | Struct-Based Pipeline Continuations | ✅ (`INext<TResponse>` struct) | ❌ (Delegate / Closure) | ❌ (Delegate / Func) |
| **Pipelines** | Zero-Allocation Pipeline Execution | ✅ (0 bytes on sync path) | ❌ | ❌ |
| **Pipelines** | Global Behaviors | ✅ (`[UseGlobalBehavior]`) | ✅ | ✅ |
| **Pipelines** | Explicit Behavior Ordering | ✅ (`[UseBehavior(order: N)]`) | ❌ | ❌ |
| **Pipelines** | Configurable Behavior Lifetime | ✅ (`[ServiceLifetime]`) | ❌ (Hardcoded transient) | ❌ |
| **Events** | Sequential Notification Execution | ✅ (Default) | ✅ | ✅ |
| **Events** | Parallel Notification Execution | ✅ (`[PublishStrategy(Parallel)]`) | ❌ | ❌ |
| **Events** | Exception Aggregation Strategy | ✅ (`SequentialAggregateExceptions`) | ❌ | ❌ |
| **Result Pattern** | First-Class Result Short-Circuiting | ✅ (`IResultFactory<T>`) | ❌ | ❌ |
| **Diagnostics** | Compile-Time Missing Handlers | ✅ (`ELM001`) | ❌ | ✅ |
| **Diagnostics** | Compile-Time Multiple Handlers | ✅ (`ELM002`, `ELM003`) | ❌ | ✅ |
| **Diagnostics** | Compile-Time Signature Validation | ✅ (`ELM004`) | ❌ | ❌ |
| **Observability** | Native OpenTelemetry Activity + Meter | ✅ (Dedicated package) | ❌ | ❌ |
