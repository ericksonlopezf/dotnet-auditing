// Copyright © Erickson Lopez. MIT License.
using BenchmarkDotNet.Attributes;
using EricksonLopez.Auditing;
using EricksonLopez.Auditing.Testing;

namespace EricksonLopez.Auditing.Benchmarks;

[MemoryDiagnoser]
public class HmacIntegrityBenchmarks
{
    private HmacAuditIntegrityService _hmac = null!;
    private AuditRecord _record = null!;
    private string _hash = null!;

    [GlobalSetup]
    public void Setup()
    {
        _hmac = new HmacAuditIntegrityService(new TestAuditIntegrityProvider());
        var baseRecord = AuditRecordBuilder.BuildDefault(
            tenantId: "tenant-benchmarks",
            actorId: "actor-123",
            resourceType: "Order",
            resourceId: "ord-9999",
            correlationId: "corr-1111");

        _hash = _hmac.ComputeHash(baseRecord, "prev-hash-0000000000000000000000000000000000000000000000000000000000000000");
        _record = baseRecord with
        {
            IntegrityHash = _hash,
            PreviousHash = "prev-hash-0000000000000000000000000000000000000000000000000000000000000000"
        };
    }

    [Benchmark]
    public string ComputeHash()
    {
        return _hmac.ComputeHash(_record, _record.PreviousHash);
    }

    [Benchmark]
    public bool VerifyHash()
    {
        return _hmac.Verify(_record);
    }
}
