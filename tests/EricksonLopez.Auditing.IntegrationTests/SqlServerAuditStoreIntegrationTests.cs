// Copyright © Erickson Lopez. MIT License.
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using AwesomeAssertions;
using Dapper;
using EricksonLopez.Auditing;
using EricksonLopez.Auditing.SqlServer;
using EricksonLopez.Auditing.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Data.SqlClient;
using Testcontainers.MsSql;
using Xunit;

namespace EricksonLopez.Auditing.IntegrationTests;

[Trait("Category", "Integration")]
public sealed class SqlServerAuditStoreIntegrationTests : IAsyncLifetime
{
    private readonly MsSqlContainer _container;
    private SqlServerAuditStore _store = null!;
    private SqlServerAuditIntegrityVerifier _verifier = null!;
    private HmacAuditIntegrityService _hmac = null!;

    public SqlServerAuditStoreIntegrationTests()
    {
        _container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest")
            .Build();
    }

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        using (var conn = new SqlConnection(_container.GetConnectionString()))
        {
            await conn.OpenAsync();
            await conn.ExecuteAsync(@"
                CREATE TABLE audit_records (
                    id UNIQUEIDENTIFIER PRIMARY KEY,
                    occurred_at DATETIMEOFFSET NOT NULL,
                    tenant_id NVARCHAR(100) NOT NULL,
                    source NVARCHAR(100) NOT NULL,
                    actor_type TINYINT NOT NULL,
                    actor_id NVARCHAR(100) NOT NULL,
                    actor_name NVARCHAR(255),
                    action_code NVARCHAR(100) NOT NULL,
                    resource_type NVARCHAR(100) NOT NULL,
                    resource_id NVARCHAR(100) NOT NULL,
                    aggregate_type NVARCHAR(100),
                    aggregate_id NVARCHAR(100),
                    outcome TINYINT NOT NULL,
                    error_code NVARCHAR(100),
                    correlation_id NVARCHAR(100),
                    causation_id NVARCHAR(100),
                    request_id NVARCHAR(100),
                    ip_address NVARCHAR(45),
                    user_agent NVARCHAR(1000),
                    changes NVARCHAR(MAX),
                    integrity_hash NVARCHAR(100),
                    previous_hash NVARCHAR(100)
                );");
        }

        var services = new ServiceCollection();
        services.AddSingleton<IAuditIntegrityProvider, TestAuditIntegrityProvider>();
        services.AddSingleton<HmacAuditIntegrityService>();

        var options = new SqlServerAuditStoreOptions
        {
            ConnectionFactory = () => new SqlConnection(_container.GetConnectionString()),
            Schema = "dbo",
            Table = "audit_records"
        };

        var provider = services.BuildServiceProvider();
        _hmac = provider.GetRequiredService<HmacAuditIntegrityService>();
        _store = new SqlServerAuditStore(options);
        _verifier = new SqlServerAuditIntegrityVerifier(options, _hmac);
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

        using (var conn = new SqlConnection(_container.GetConnectionString()))
        {
            await conn.OpenAsync();
            await conn.ExecuteAsync("UPDATE dbo.audit_records SET integrity_hash = 'tampered' WHERE id = @Id", new { Id = r1.Id });
        }

        var result = await _verifier.VerifyChainAsync(tenant, DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        result.IsValid.Should().BeFalse();
        result.FirstFailedRecordId.Should().Be(r1.Id);
    }
}
