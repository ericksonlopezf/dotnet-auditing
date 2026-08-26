// Copyright © Erickson Lopez. MIT License.
using BenchmarkDotNet.Attributes;
using EricksonLopez.Auditing;

namespace EricksonLopez.Auditing.Benchmarks;

[MemoryDiagnoser]
public class AuditIdBenchmarks
{
    [Benchmark]
    public Guid GenerateAuditId()
    {
        return AuditId.NewId();
    }
}
