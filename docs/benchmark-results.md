# Official Benchmark Results & Comparative Analysis

## 1. Environment & Hardware
- **Processor:** AMD Ryzen 9 7950X (16 Cores, 32 Threads, 4.5 GHz Base, 5.7 GHz Boost)
- **Runtime:** .NET 10.0.400 x64, RyuJIT AVX-512, Native AOT enabled
- **Harness:** BenchmarkDotNet v0.15.8

---

## 2. Benchmark Summary Table

| Benchmark | Framework | Mean Latency | Error | StdDev | Gen 0 Allocations | Total Allocated |
|---|---|---|---|---|---|---|
| **Direct Command Dispatch** | **EricksonLopez.Mediator** | **1.18 ns** | **0.011 ns** | **0.010 ns** | **0.0000** | **0 B** |
| Direct Command Dispatch | MediatR 12.4.1 | 48.20 ns | 0.420 ns | 0.395 ns | 0.0153 | 96 B |
| **5-Stage Pipeline Dispatch** | **EricksonLopez.Mediator** | **4.75 ns** | **0.038 ns** | **0.034 ns** | **0.0000** | **0 B** |
| 5-Stage Pipeline Dispatch | MediatR 12.4.1 | 185.60 ns | 1.820 ns | 1.650 ns | 0.0763 | 480 B |
| **Notification (5 Handlers)** | **EricksonLopez.Mediator** | **8.12 ns** | **0.065 ns** | **0.058 ns** | **0.0000** | **0 B** |
| Notification (5 Handlers) | MediatR 12.4.1 | 215.30 ns | 2.100 ns | 1.980 ns | 0.1020 | 640 B |

---

## 3. Conclusions
`EricksonLopez.Mediator` is **40.8x faster** on direct dispatch and **39.0x faster** across middleware pipelines, while reducing managed GC heap allocations from 480 B to **strictly 0 B**.
