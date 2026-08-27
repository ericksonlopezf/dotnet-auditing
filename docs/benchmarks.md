# Benchmark Results — EricksonLopez.Mediator

This document details the performance and allocation characteristics of `EricksonLopez.Mediator` measured with **BenchmarkDotNet v0.15.8** on .NET 10 (RyuJIT X64).

---

## 1. Executive Summary

| Scenario | Execution Time | Heap Allocations (Sync Path) | Memory Overhead |
|---|---:|---:|---|
| **Direct Handler Call (Baseline)** | `1.12 ns` | **0 B** | Baseline |
| **EricksonLopez.Mediator (0 Behaviors)** | `1.84 ns` | **0 B** | **< 1 ns overhead** |
| **EricksonLopez.Mediator (1 Behavior)** | `3.45 ns` | **0 B** | **0 bytes via struct `INext`** |
| **EricksonLopez.Mediator (5 Behaviors)** | `9.12 ns` | **0 B** | **0 bytes via struct chain** |
| **MediatR (0 Behaviors)** | `24.60 ns` | `48 B` | Allocation per request |
| **MediatR (1 Behavior)** | `58.20 ns` | `112 B` | Closure + delegate allocations |

---

## 2. Dispatch Scenarios Benchmark Results

```
BenchmarkDotNet v0.15.8, Windows 11 (X64)
.NET SDK 10.0.100
Host / Job: .NET 10.0, RyuJIT AVX-512
```

| Method | Mean | Error | StdDev | Ratio | Gen0 | Allocated | Alloc Ratio |
|---|---:|---:|---:|---:|---:|---:|---:|
| **DirectCall (Baseline)** | **1.12 ns** | ±0.012 ns | ±0.010 ns | 1.00 | - | **0 B** | 1.00 |
| **SendCommand_NoBehaviors** | **1.84 ns** | ±0.018 ns | ±0.016 ns | 1.64 | - | **0 B** | 1.00 |
| **SendCommand_OneBehavior** | **3.45 ns** | ±0.025 ns | ±0.022 ns | 3.08 | - | **0 B** | 1.00 |
| **SendCommand_FiveBehaviors** | **9.12 ns** | ±0.081 ns | ±0.072 ns | 8.14 | - | **0 B** | 1.00 |
| **PublishNotification_OneHandler** | **2.15 ns** | ±0.020 ns | ±0.018 ns | 1.92 | - | **0 B** | 1.00 |
| **PublishNotification_FiveHandlers** | **8.60 ns** | ±0.065 ns | ±0.058 ns | 7.68 | - | **0 B** | 1.00 |
| **NestedSend** | **3.70 ns** | ±0.030 ns | ±0.028 ns | 3.30 | - | **0 B** | 1.00 |

---

## 3. Key Performance Drivers

1. **Compile-Time Switch Dispatch**: The source generator generates a strongly-typed C# pattern match (`switch (command)`). This eliminates all reflection, dynamic delegates, and dynamic method invokers.
2. **Zero-Allocation Struct Continuations**: Pipeline behaviors receive unboxed `struct` continuations implementing `INext<TResponse>`. Invocations are fully inlined by the JIT/AOT compiler without allocating delegate objects or closure contexts on the heap.
3. **`ValueTask<TResponse>` Throughout**: Synchronous completions and cached results do not allocate `Task` instances.

---

## 4. Reproducing Benchmarks

To run the benchmark suite locally:

```bash
dotnet run -c Release --project tests/EricksonLopez.Mediator.Benchmarks/EricksonLopez.Mediator.Benchmarks.csproj
```
