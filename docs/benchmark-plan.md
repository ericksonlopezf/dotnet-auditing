# Benchmark Strategy & Performance Verification Plan

## 1. Scope & Objectives
The benchmark suite located in `benchmarks/EricksonLopez.Mediator.Benchmarks` measures dispatch latency, memory allocations, and throughput comparing `EricksonLopez.Mediator` against `MediatR 12.x`.

---

## 2. Benchmark Scenarios

### Benchmark 01: Direct Command Dispatch
- Measures bare dispatch latency for `ISender.Send(command)` without behaviors.
- Verifies sub-nanosecond direct handler invocation.

### Benchmark 02: 5-Stage Middleware Pipeline
- Measures overhead through an 8-stage pipeline (Logging, Validation, Metrics, Polly Resilience, RateLimiting).
- Verifies zero heap allocation invariant across struct `INext<TResponse>` continuations.

### Benchmark 03: Notification Publishing
- Measures broadcast latency across 5 registered `INotificationHandler` subscribers.
- Compares sequential vs parallel publish strategies.

### Benchmark 04: Asynchronous Streaming Queries
- Measures memory throughput while streaming 10,000 records via `IStreamQuery<T>`.
