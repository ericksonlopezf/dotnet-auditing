// Copyright © Erickson Lopez. MIT License.
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using AwesomeAssertions;
using Dapper;
using EricksonLopez.Auditing;
using EricksonLopez.Auditing.PostgreSql;
using EricksonLopez.Auditing.Testing;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace EricksonLopez.Auditing.IntegrationTests;

public sealed class PostgreSqlAuditStoreIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container;
    private PostgreSqlAuditStore _store = null!;
    private PostgreSqlAuditIntegrityVerifier _verifier = null!;
    private HmacAuditIntegrityService _hmac = null!;

    public PostgreSqlAuditStoreIntegrationTests()
    {
        _container = new PostgreSqlBuilder("postgres:15-alpine")
            .Build();
    }

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        using (var conn = new NpgsqlConnection(_container.GetConnectionString()))
        {
            await conn.OpenAsync();
            await conn.ExecuteAsync(@"
                CREATE TABLE audit_records (
                    id UUID PRIMARY KEY,
                    occurred_at TIMESTAMPTZ NOT NULL,
                    tenant_id VARCHAR(100) NOT NULL,
                    source VARCHAR(100) NOT NULL,
                    actor_type SMALLINT NOT NULL,
                    actor_id VARCHAR(100) NOT NULL,
                    actor_name VARCHAR(255),
                    action_code VARCHAR(100) NOT NULL,
                    resource_type VARCHAR(100) NOT NULL,
                    resource_id VARCHAR(100) NOT NULL,
                    aggregate_type VARCHAR(100),
                    aggregate_id VARCHAR(100),
                    outcome SMALLINT NOT NULL,
                    error_code VARCHAR(100),
                    correlation_id VARCHAR(100),
                    causation_id VARCHAR(100),
                    request_id VARCHAR(100),
                    ip_address VARCHAR(45),
                    user_agent VARCHAR(1000),
                    changes JSONB,
                    integrity_hash VARCHAR(100),
                    previous_hash VARCHAR(100)
                );");
        }

        var services = new ServiceCollection();
        services.AddSingleton<IAuditIntegrityProvider, TestAuditIntegrityProvider>();
        services.AddSingleton<HmacAuditIntegrityService>();

        var options = new PostgreSqlAuditStoreOptions
        {
            ConnectionFactory = () => new NpgsqlConnection(_container.GetConnectionString()),
            Schema = "public",
            Table = "audit_records"
        };

        var provider = services.BuildServiceProvider();
        _hmac = provider.GetRequiredService<HmacAuditIntegrityService>();
        _store = new PostgreSqlAuditStore(options);
        _verifier = new PostgreSqlAuditIntegrityVerifier(options, _hmac);
    }

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }

    [Fact(Timeout = 30000)]
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

    [Fact(Timeout = 30000)]
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

    [Fact(Timeout = 30000)]
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

    [Fact(Timeout = 30000)]
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

        using (var conn = new NpgsqlConnection(_container.GetConnectionString()))
        {
            await conn.OpenAsync();
            await conn.ExecuteAsync("UPDATE public.audit_records SET integrity_hash = 'tampered' WHERE id = @Id", new { Id = r1.Id });
        }

        var result = await _verifier.VerifyChainAsync(tenant, DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        result.IsValid.Should().BeFalse();
        result.FirstFailedRecordId.Should().Be(r1.Id);
    }
}
