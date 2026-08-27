# Performance Principles & Low-Latency Architecture

## 1. Zero-Allocation Struct Pipelines
Every request pipeline middleware implements `where TNext : struct, INext<TResponse>`. Struct continuations are allocated directly on the call stack or stored in CPU registers, resulting in **0 B managed heap allocations**.

## 2. Direct Value Method Invocation
The Roslyn source generator generates static method dispatchers that call concrete handler `Handle()` methods directly, completely bypassing `System.Reflection.MethodInfo.Invoke` and boxing conversions.
