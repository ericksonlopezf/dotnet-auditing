# Algorithmic & Cyclomatic Complexity Audit — EricksonLopez.Mediator

This document provides a review of Cyclomatic Complexity (CC), Cohesion, and Algorithmic Time Complexity (Big-O) for the runtime dispatching and compilation pipelines of `EricksonLopez.Mediator`.

---

## 1. Runtime Dispatch Complexity (Big-O)

All runtime dispatching in `EricksonLopez.Mediator` is statically generated at compile time:

| Operation | Time Complexity | Allocations | Details |
|---|---|---|---|
| **Command Dispatch (`Send`)** | **`O(1)`** | **0 B** | Direct C# `switch` pattern matching on request type symbol. |
| **Query Dispatch (`Send`)** | **`O(1)`** | **0 B** | Direct C# `switch` pattern matching on query type symbol. |
| **Pipeline Continuation (`INext.InvokeAsync`)** | **`O(1)`** | **0 B** | Unboxed `struct` method invocation inlined by RyuJIT / Native AOT. |
| **Sequential Notification (`Publish`)** | **`O(H)`** | **0 B** | Linear execution over $H$ registered handler instances. |
| **Parallel Notification (`Publish`)** | **`O(H)`** | `O(H)` | `Task.WhenAll` over $H$ handler tasks. |

---

## 2. Runtime Cyclomatic Complexity (CC)

*Target: Cyclomatic Complexity < 10 for all runtime methods, Maintainability Index > 85.*

| Runtime Component | Target Method | Max CC | Average CC | Assessment |
|---|---|---|---|---|
| **`GeneratedMediator`** | `Send<TResponse>(ICommand)` | 3 | 1.5 | **PASS**. Flat `switch` statement with direct struct instantiation. |
| **`GeneratedMediator`** | `Publish<TNotification>` | 3 | 1.8 | **PASS**. Clean conditional branch on publication strategy. |
| **`OpenTelemetryBehavior`** | `Handle<TNext>` | 3 | 2.0 | **PASS**. Flat activity wrapping and duration timing. |
| **`ResultFactory`** | `CreateFailure` | 1 | 1.0 | **PASS**. Direct constructor invocation on `Result<T>`. |

---

## 3. Compile-Time Incremental Generator Complexity

- **Symbol Discovery**: `O(N)` where $N$ is the number of syntax nodes implementing mediator marker interfaces.
- **Model Building (`MediatorModelBuilder`)**: `O(T + B)` where $T$ is discovered types and $B$ is behaviors.
- **Code Emission (`DispatcherGenerator`)**: Linear `O(H)` StringBuilder appends.
