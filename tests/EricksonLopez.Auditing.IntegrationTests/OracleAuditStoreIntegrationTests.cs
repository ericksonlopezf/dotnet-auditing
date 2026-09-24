// Copyright © Erickson Lopez. MIT License.
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using AwesomeAssertions;
using Dapper;
using EricksonLopez.Auditing;
using EricksonLopez.Auditing.Oracle;
using EricksonLopez.Auditing.Testing;
using Microsoft.Extensions.DependencyInjection;
using Oracle.ManagedDataAccess.Client;
using Testcontainers.Oracle;
using Xunit;

namespace EricksonLopez.Auditing.IntegrationTests;

[Trait("Category", "Integration")]
public sealed class OracleAuditStoreIntegrationTests : IClassFixture<OracleFixture>
{
    private readonly OracleFixture _fixture;
    private readonly OracleAuditStore _store;
    private readonly OracleAuditIntegrityVerifier _verifier;
    private readonly HmacAuditIntegrityService _hmac;

    public OracleAuditStoreIntegrationTests(OracleFixture fixture)
    {
        _fixture = fixture;
        var services = new ServiceCollection();
        services.AddSingleton<IAuditIntegrityProvider, TestAuditIntegrityProvider>();
        services.AddSingleton<IAuditHashAlgorithm, HmacSha256AuditHashAlgorithm>();
        services.AddSingleton<HmacAuditIntegrityService>();

        var options = new OracleAuditStoreOptions
        {
            ConnectionFactory = () => new OracleConnection(fixture.Container.GetConnectionString()),
            Schema = string.Empty,
            Table = "AUDIT_RECORDS"
        };

        var provider = services.BuildServiceProvider();
        _hmac = provider.GetRequiredService<HmacAuditIntegrityService>();
        _store = new OracleAuditStore(options);
        _verifier = new OracleAuditIntegrityVerifier(options, _hmac);
    }

    [Fact(Timeout = 300000)]
    public async Task AppendAndQuery_WithChanges_Succeeds()
    {
        var record = Builders.Build(tenantId: "tenant-changes") with
        {
            Changes = new List<AuditChange>
            {
                new AuditChange("Status", "Pending", "Active"),
                AuditChange.Redacted("SecretKey")
            }
        };

        await _store.AppendAsync(record);

        var page = await _store.QueryAsync(new AuditQuery { TenantId = "tenant-changes" });
        page.Records.Should().HaveCount(1);

        var fetched = page.Records[0];
        fetched.Changes.Should().HaveCount(2);
        fetched.Changes[0].Field.Should().Be("Status");
        fetched.Changes[0].OldValue.Should().Be("Pending");
        fetched.Changes[0].NewValue.Should().Be("Active");
        fetched.Changes[1].IsRedacted.Should().BeTrue();
    }

    [Fact(Timeout = 300000)]
    public async Task AppendBatchAsync_ValidRecords_Succeeds()
    {
        var tenant = "t-batch";
        var r1 = Builders.Build(tenantId: tenant);
        var r2 = Builders.Build(tenantId: tenant);
        var r3 = Builders.Build(tenantId: tenant);

        await _store.AppendBatchAsync(new[] { r1, r2, r3 });

        var page = await _store.QueryAsync(new AuditQuery { TenantId = tenant });
        page.Records.Should().HaveCount(3);
    }

    [Fact(Timeout = 300000)]
    public async Task VerifyChain_ValidChain_ReturnsTrue()
    {
        var tenant = "t-chain";
        var r1 = Builders.Build(tenantId: tenant);
        var hash1 = _hmac.ComputeHash(r1, null);
        r1 = r1 with { IntegrityHash = hash1, PreviousHash = null };

        var r2 = Builders.Build(tenantId: tenant) with { OccurredAt = r1.OccurredAt.AddSeconds(1), PreviousHash = hash1 };
        var hash2 = _hmac.ComputeHash(r2, hash1);
        r2 = r2 with { IntegrityHash = hash2 };

        var r3 = Builders.Build(tenantId: tenant) with { OccurredAt = r1.OccurredAt.AddSeconds(2), PreviousHash = hash2 };
        var hash3 = _hmac.ComputeHash(r3, hash2);
        r3 = r3 with { IntegrityHash = hash3 };

        await _store.AppendBatchAsync(new[] { r1, r2, r3 });

        var result = await _verifier.VerifyChainAsync(tenant, DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));

        result.IsValid.Should().BeTrue();
        result.VerifiedCount.Should().Be(3);
    }

    [Fact(Timeout = 300000)]
    public async Task VerifyChain_BrokenChain_ReturnsFalse()
    {
        var tenant = "t-broken";
        var r1 = Builders.Build(tenantId: tenant);
        var hash1 = _hmac.ComputeHash(r1, null);
        r1 = r1 with { IntegrityHash = hash1, PreviousHash = null };

        var r2 = Builders.Build(tenantId: tenant) with { OccurredAt = r1.OccurredAt.AddSeconds(1), PreviousHash = hash1 };
        var hash2 = _hmac.ComputeHash(r2, hash1);
        r2 = r2 with { IntegrityHash = hash2 };

        await _store.AppendBatchAsync(new[] { r1, r2 });

        using (var conn = new OracleConnection(_fixture.Container.GetConnectionString()))
        {
            await conn.OpenAsync();
            await conn.ExecuteAsync("UPDATE AUDIT_RECORDS SET INTEGRITY_HASH = 'tampered' WHERE ID = :Id", new { Id = r1.Id.ToString("D") });
        }

        var result = await _verifier.VerifyChainAsync(tenant, DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        result.IsValid.Should().BeFalse();
        result.FirstFailedRecordId.Should().Be(r1.Id);
    }
}




