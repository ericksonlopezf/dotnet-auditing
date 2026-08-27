// Copyright © Erickson Lopez. MIT License.
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using EricksonLopez.Auditing;
using EricksonLopez.Auditing.Testing;

namespace EricksonLopez.Auditing.Benchmarks;

[MemoryDiagnoser]
public class InMemoryStoreBenchmarks
{
    private InMemoryAuditStore _store = null!;
    private AuditRecord _record = null!;
    private AuditQuery _query = null!;

    [GlobalSetup]
    public async Task Setup()
    {
        _store = new InMemoryAuditStore();
        for (int i = 0; i < 500; i++)
        {
            await _store.AppendAsync(AuditRecordBuilder.BuildDefault(
                tenantId: "tenant-bench",
                actorId: $"user-{i % 10}",
                resourceType: "Invoice",
                resourceId: $"inv-{i}"));
        }

        _record = AuditRecordBuilder.BuildDefault(tenantId: "tenant-bench");
        _query = new AuditQuery
        {
            TenantId = "tenant-bench",
            ResourceType = "Invoice",
            PageSize = 50
        };
    }

    [Benchmark]
    public async ValueTask AppendSingleRecord()
    {
        await _store.AppendAsync(_record);
    }

    [Benchmark]
    public async ValueTask<AuditQueryResult> QueryWithKeysetFilter()
    {
        return await _store.QueryAsync(_query);
    }
}
