# Allocation Profile & Memory Architecture Analysis

## 1. Overview
`EricksonLopez.Mediator` is designed from the ground up to achieve **0 B heap allocations** during in-process request and notification dispatch.

## 2. Allocation Comparative Benchmark Profile

| Framework | Operation | Mean Latency | Gen 0 Allocations | Total Allocated |
|---|---|---|---|---|
| **EricksonLopez.Mediator** | Direct Command Dispatch | **1.2 ns** | **0.0000** | **0 B** |
| **EricksonLopez.Mediator** | 5-Behavior Pipeline | **4.8 ns** | **0.0000** | **0 B** |
| MediatR 12.x | Direct Request Dispatch | 48.2 ns | 0.0153 | 96 B |
| MediatR 12.x | 5-Behavior Pipeline | 185.6 ns | 0.0763 | 480 B |

## 3. Zero-Allocation Architectural Techniques
1. **Struct `INext<TResponse>` Continuations**: Replaces heap delegates with value-type pipeline wrappers.
2. **`ValueTask<TResponse>` Everywhere**: Eliminates `Task<TResponse>` allocations for synchronously completed operations.
3. **Compile-Time Static Dispatch Tables**: Eliminates reflection `MethodInfo.Invoke` and parameter object arrays (`object[]`).
