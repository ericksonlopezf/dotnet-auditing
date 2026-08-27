# Competitive Evidence & Benchmarking Data

## 1. Concrete Benchmark Measurements

Measurements executed via BenchmarkDotNet on AMD Ryzen 9 7950X (.NET 10.0 Native AOT):

```
BenchmarkDotNet v0.15.8, Windows 11 (10.0.22631.4317)
AMD Ryzen 9 7950X 16-Core Processor, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.400
  [Host]     : .NET 10.0.0, X64 RyuJIT AVX-512
  DefaultJob : .NET 10.0.0, X64 RyuJIT AVX-512 NativeAOT
```

| Method | Mean | Error | StdDev | Ratio | Gen0 | Allocated |
|---|---|---|---|---|---|---|
| **EricksonLopez.Mediator (Direct)** | **1.18 ns** | **0.011 ns** | **0.010 ns** | **1.00** | **-** | **0 B** |
| MediatR 12.x (Direct) | 48.20 ns | 0.420 ns | 0.395 ns | 40.85 | 0.0153 | 96 B |
| **EricksonLopez.Mediator (5 Behaviors)** | **4.75 ns** | **0.038 ns** | **0.034 ns** | **1.00** | **-** | **0 B** |
| MediatR 12.x (5 Behaviors) | 185.60 ns | 1.820 ns | 1.650 ns | 39.07 | 0.0763 | 480 B |

---

## 2. Key Empirical Findings
1. Pipeline execution through 5 behaviors takes **4.75 nanoseconds** with **0 bytes allocated**.
2. Zero memory allocations prevent GC Gen 0 pressure during high-throughput microservice request spikes (500k+ req/sec).
