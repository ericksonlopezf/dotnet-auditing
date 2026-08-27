// Copyright © Erickson Lopez. MIT License.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using EricksonLopez.Auditing;
using EricksonLopez.Auditing.Sqlite;
using EricksonLopez.Auditing.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EricksonLopez.Auditing.IntegrationTests;

[Trait("Category", "Integration")]
public sealed class SqliteAuditStoreIntegrationTests : IDisposable
{
    private readonly string _dbPath;
    private readonly SqliteAuditStore _store;
    private readonly HmacAuditIntegrityService _hmac;
    private readonly SqliteAuditIntegrityVerifier _verifier;

    public SqliteAuditStoreIntegrationTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"audit_test_{Guid.NewGuid():N}.db");
        using (var initConn = new SqliteConnection($"Data Source={_dbPath}"))
        {
            initConn.Open();
            using var cmd = initConn.CreateCommand();
            cmd.CommandText = """
                CREATE TABLE IF NOT EXISTS audit_records (
                    id              TEXT    NOT NULL,
                    occurred_at     TEXT    NOT NULL,
                    tenant_id       TEXT    NOT NULL,
                    source          TEXT    NOT NULL,
                    actor_type      INTEGER NOT NULL,
                    actor_id        TEXT    NOT NULL,
                    actor_name      TEXT    NULL,
                    action_code     TEXT    NOT NULL,
                    resource_type   TEXT    NOT NULL,
                    resource_id     TEXT    NOT NULL,
                    aggregate_type  TEXT    NULL,
                    aggregate_id    TEXT    NULL,
                    outcome         INTEGER NOT NULL,
                    error_code      TEXT    NULL,
                    correlation_id  TEXT    NULL,
                    causation_id    TEXT    NULL,
                    request_id      TEXT    NULL,
                    ip_address      TEXT    NULL,
                    user_agent      TEXT    NULL,
                    changes         TEXT    NULL,
                    integrity_hash  TEXT    NULL,
                    previous_hash   TEXT    NULL,
                    PRIMARY KEY (occurred_at ASC, id ASC)
                );
                """;
            cmd.ExecuteNonQuery();
        }

        var options = new SqliteAuditStoreOptions
        {
            ConnectionFactory = () => new SqliteConnection($"Data Source={_dbPath}"),
            Table = "audit_records"
        };

        _store = new SqliteAuditStore(options);
        _hmac = new HmacAuditIntegrityService(new TestAuditIntegrityProvider());
        _verifier = new SqliteAuditIntegrityVerifier(options, _hmac);
    }

    public void Dispose()
    {
        if (File.Exists(_dbPath))
        {
            try { File.Delete(_dbPath); } catch { }
        }
    }

    [Fact(Timeout = 30000)]
    public async Task Sqlite_AppendAndQuery_SingleRecord_Succeeds()
    {
        var record = AuditRecordBuilder.BuildDefault(tenantId: "tenant-sql-1", actorId: "alice", resourceId: "res-1");

        await _store.AppendAsync(record);

        var result = await _store.QueryAsync(new AuditQuery
        {
            TenantId = "tenant-sql-1"
        });

        result.Records.Should().HaveCount(1);
        result.Records[0].Id.Should().Be(record.Id);
        result.Records[0].Actor.Id.Should().Be("alice");
        result.Records[0].Context.TenantId.Should().Be("tenant-sql-1");
    }

    [Fact(Timeout = 30000)]
    public async Task Sqlite_TenantIsolation_TenantACannotSeeTenantB()
    {
        var recordA = AuditRecordBuilder.BuildDefault(tenantId: "tenant-isolated-a", resourceId: "res-a");
        var recordB = AuditRecordBuilder.BuildDefault(tenantId: "tenant-isolated-b", resourceId: "res-b");

        await _store.AppendAsync(recordA);
        await _store.AppendAsync(recordB);

        var resultA = await _store.QueryAsync(new AuditQuery { TenantId = "tenant-isolated-a" });
        var resultB = await _store.QueryAsync(new AuditQuery { TenantId = "tenant-isolated-b" });

        resultA.Records.Should().HaveCount(1);
        resultA.Records[0].Resource.Id.Should().Be("res-a");

        resultB.Records.Should().HaveCount(1);
        resultB.Records[0].Resource.Id.Should().Be("res-b");
    }

    [Fact(Timeout = 30000)]
    public async Task Sqlite_KeysetPagination_PagesAccurately()
    {
        var tenant = "tenant-pagination";
        for (int i = 0; i < 5; i++)
        {
            await Task.Delay(2); // ensure distinct timestamps
            await _store.AppendAsync(AuditRecordBuilder.BuildDefault(tenantId: tenant, resourceId: $"res-{i}"));
        }

        var page1 = await _store.QueryAsync(new AuditQuery { TenantId = tenant, PageSize = 2 });
        page1.Records.Should().HaveCount(2);
        page1.HasMore.Should().BeTrue();
        page1.NextCursorId.Should().NotBeNull();

        var page2 = await _store.QueryAsync(new AuditQuery
        {
            TenantId = tenant,
            PageSize = 2,
            AfterRecordId = page1.NextCursorId
        });
        page2.Records.Should().HaveCount(2);
        page2.HasMore.Should().BeTrue();

        var page3 = await _store.QueryAsync(new AuditQuery
        {
            TenantId = tenant,
            PageSize = 2,
            AfterRecordId = page2.NextCursorId
        });
        page3.Records.Should().HaveCount(1);
        page3.HasMore.Should().BeFalse();
        page3.NextCursorId.Should().BeNull();
    }

    [Fact(Timeout = 30000)]
    public async Task Sqlite_IntegrityVerification_ValidChain_ReturnsTrue()
    {
        var tenant = "tenant-integrity-valid";
        var r1 = AuditRecordBuilder.BuildDefault(tenantId: tenant, resourceId: "res-1");
        var hash1 = _hmac.ComputeHash(r1, null);
        var signed1 = r1 with { IntegrityHash = hash1, PreviousHash = null };
        await _store.AppendAsync(signed1);

        await Task.Delay(2);

        var r2 = AuditRecordBuilder.BuildDefault(tenantId: tenant, resourceId: "res-2");
        var hash2 = _hmac.ComputeHash(r2, hash1);
        var signed2 = r2 with { IntegrityHash = hash2, PreviousHash = hash1 };
        await _store.AppendAsync(signed2);

        var verifyResult = await _verifier.VerifyChainAsync(
            tenant,
            DateTimeOffset.UtcNow.AddMinutes(-5),
            DateTimeOffset.UtcNow.AddMinutes(5));

        verifyResult.IsValid.Should().BeTrue();
        verifyResult.VerifiedCount.Should().Be(2);
    }

    [Fact(Timeout = 30000)]
    public async Task Sqlite_IntegrityVerification_TamperedContent_Detected()
    {
        var tenant = "tenant-integrity-tampered";
        var r1 = AuditRecordBuilder.BuildDefault(tenantId: tenant, resourceId: "res-1");
        var hash1 = _hmac.ComputeHash(r1, null);
        var signed1 = r1 with { IntegrityHash = hash1, PreviousHash = null };
        await _store.AppendAsync(signed1);

        // Tamper directly in the database
        using (var tamperConn = new SqliteConnection($"Data Source={_dbPath}"))
        {
            tamperConn.Open();
            using var cmd = tamperConn.CreateCommand();
            cmd.CommandText = $"UPDATE audit_records SET action_code = 'DELETE_EVERYTHING' WHERE id = '{signed1.Id:D}';";
            cmd.ExecuteNonQuery();
        }

        var verifyResult = await _verifier.VerifyChainAsync(
            tenant,
            DateTimeOffset.UtcNow.AddMinutes(-5),
            DateTimeOffset.UtcNow.AddMinutes(5));

        verifyResult.IsValid.Should().BeFalse();
        verifyResult.FirstFailedRecordId.Should().Be(signed1.Id);
        verifyResult.FailureReason.Should().Contain("tampered");
    }

    [Fact(Timeout = 30000)]
    public async Task Sqlite_IntegrityVerification_ChainBreak_Detected()
    {
        var tenant = "tenant-chainbreak";
        var r1 = AuditRecordBuilder.BuildDefault(tenantId: tenant, resourceId: "res-1");
        var hash1 = _hmac.ComputeHash(r1, null);
        await _store.AppendAsync(r1 with { IntegrityHash = hash1, PreviousHash = null });

        await Task.Delay(2);

        var r2 = AuditRecordBuilder.BuildDefault(tenantId: tenant, resourceId: "res-2");
        var hash2 = _hmac.ComputeHash(r2, "wrong_prev");
        await _store.AppendAsync(r2 with { IntegrityHash = hash2, PreviousHash = "wrong_prev" });

        var verifyResult = await _verifier.VerifyChainAsync(
            tenant,
            DateTimeOffset.UtcNow.AddMinutes(-5),
            DateTimeOffset.UtcNow.AddMinutes(5));

        verifyResult.IsValid.Should().BeFalse();
        verifyResult.FailureReason.Should().Contain("Chain break: previous_hash does not match predecessor");
    }

    [Fact(Timeout = 30000)]
    public async Task Sqlite_AppendAndQuery_WithChanges_Succeeds()
    {
        var record = AuditRecordBuilder.Create()
            .WithTenant("tenant-changes")
            .AddChange("Status", "Pending", "Active")
            .AddRedactedChange("SecretKey")
            .Build();

        await _store.AppendAsync(record);
        var result = await _store.QueryAsync(new AuditQuery { TenantId = "tenant-changes" });

        var changes = result.Records[0].Changes;
        changes.Should().NotBeNull();
        changes.Should().HaveCount(2);
        changes![0].Field.Should().Be("Status");
        changes[1].IsRedacted.Should().BeTrue();
    }

    [Fact(Timeout = 30000)]
    public async Task Sqlite_AppendBatchAsync_ValidRecords_Succeeds()
    {
        var records = new List<AuditRecord>
        {
            AuditRecordBuilder.BuildDefault(tenantId: "tenant-batch", resourceId: "res-1"),
            AuditRecordBuilder.BuildDefault(tenantId: "tenant-batch", resourceId: "res-2")
        };

        await _store.AppendBatchAsync(records);

        var result = await _store.QueryAsync(new AuditQuery { TenantId = "tenant-batch" });
        result.Records.Should().HaveCount(2);
    }

    [Fact(Timeout = 30000)]
    public async Task Sqlite_AppendBatchAsync_NullRecords_Throws()
    {
        Func<Task> act = async () => await _store.AppendBatchAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact(Timeout = 30000)]
    public async Task Sqlite_AppendBatchAsync_EmptyRecords_DoesNothing()
    {
        var records = new List<AuditRecord>();
        Func<Task> act = async () => await _store.AppendBatchAsync(records);
        await act.Should().NotThrowAsync();
    }

    [Fact(Timeout = 30000)]
    public async Task Sqlite_AppendBatchAsync_DifferentTenants_Throws()
    {
        var records = new List<AuditRecord>
        {
            AuditRecordBuilder.BuildDefault(tenantId: "tenant-a", resourceId: "res-1"),
            AuditRecordBuilder.BuildDefault(tenantId: "tenant-b", resourceId: "res-2")
        };

        Func<Task> act = async () => await _store.AppendBatchAsync(records);
        var ex = await act.Should().ThrowAsync<InvalidOperationException>();
        ex.Which.Message.Should().Be("All records in a batch must belong to the same tenant. Split cross-tenant records into separate batch operations.");
    }

    [Fact(Timeout = 30000)]
    public async Task Sqlite_NullArguments_ThrowExceptions()
    {
        var options = new SqliteAuditStoreOptions
        {
            ConnectionFactory = () => new SqliteConnection($"Data Source={_dbPath}"),
            Table = "audit_records"
        };

        Assert.Throws<ArgumentNullException>(() => new SqliteAuditStore(null!));
        Assert.Throws<ArgumentNullException>(() => new SqliteAuditIntegrityVerifier(null!, _hmac));
        Assert.Throws<ArgumentNullException>(() => new SqliteAuditIntegrityVerifier(options, null!));

        Func<Task> nullAppend = async () => await _store.AppendAsync(null!);
        await nullAppend.Should().ThrowAsync<ArgumentNullException>();

        Func<Task> nullQuery = async () => await _store.QueryAsync(null!);
        await nullQuery.Should().ThrowAsync<ArgumentNullException>();

        Func<Task> invalidPageSizeZero = async () => await _store.QueryAsync(new AuditQuery { TenantId = "t", PageSize = 0 });
        var exZero = await invalidPageSizeZero.Should().ThrowAsync<ArgumentOutOfRangeException>();
        exZero.Which.Message.Should().Contain("PageSize must be between 1 and 1000.");

        Func<Task> invalidPageSizeTooBig = async () => await _store.QueryAsync(new AuditQuery { TenantId = "t", PageSize = 1001 });
        var exBig = await invalidPageSizeTooBig.Should().ThrowAsync<ArgumentOutOfRangeException>();
        exBig.Which.Message.Should().Contain("PageSize must be between 1 and 1000.");

        Func<Task> nullTenantVerifier = async () => await _verifier.VerifyChainAsync(null!, DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow);
        await nullTenantVerifier.Should().ThrowAsync<ArgumentException>();

        Func<Task> emptyTenantVerifier = async () => await _verifier.VerifyChainAsync("", DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow);
        await emptyTenantVerifier.Should().ThrowAsync<ArgumentException>();
    }

    [Fact(Timeout = 30000)]
    public async Task Sqlite_Query_AllFilters_FilterAccurately()
    {
        var tenant = "tenant-all-filters";
        var now = DateTimeOffset.UtcNow;
        var r1 = AuditRecordBuilder.Create()
            .WithTenant(tenant)
            .WithActor(AuditActorType.User, "alice", "Alice")
            .WithResource("Order", "ord-1")
            .WithOutcome(AuditOutcome.Success)
            .WithCorrelationId("c-1")
            .WithOccurredAt(now.AddMinutes(-10))
            .WithAction(AuditAction.Create)
            .Build();

        var r2 = AuditRecordBuilder.Create()
            .WithTenant(tenant)
            .WithActor(AuditActorType.User, "bob", "Bob")
            .WithResource("Invoice", "inv-2")
            .WithOutcome(AuditOutcome.Failure)
            .WithCorrelationId("c-2")
            .WithOccurredAt(now.AddMinutes(-5))
            .WithAction(AuditAction.Delete)
            .Build();

        await _store.AppendBatchAsync(new[] { r1, r2 });

        var byActor = await _store.QueryAsync(new AuditQuery { TenantId = tenant, ActorId = "alice" });
        byActor.Records.Should().HaveCount(1);
        byActor.Records[0].Id.Should().Be(r1.Id);

        var byAction = await _store.QueryAsync(new AuditQuery { TenantId = tenant, ActionCode = "Delete" });
        byAction.Records.Should().HaveCount(1);
        byAction.Records[0].Id.Should().Be(r2.Id);

        var byType = await _store.QueryAsync(new AuditQuery { TenantId = tenant, ResourceType = "Invoice" });
        byType.Records.Should().HaveCount(1);
        byType.Records[0].Id.Should().Be(r2.Id);

        var byResourceId = await _store.QueryAsync(new AuditQuery { TenantId = tenant, ResourceId = "ord-1" });
        byResourceId.Records.Should().HaveCount(1);
        byResourceId.Records[0].Id.Should().Be(r1.Id);

        var byOutcome = await _store.QueryAsync(new AuditQuery { TenantId = tenant, Outcome = AuditOutcome.Failure });
        byOutcome.Records.Should().HaveCount(1);
        byOutcome.Records[0].Id.Should().Be(r2.Id);

        var byCorrelation = await _store.QueryAsync(new AuditQuery { TenantId = tenant, CorrelationId = "c-1" });
        byCorrelation.Records.Should().HaveCount(1);
        byCorrelation.Records[0].Id.Should().Be(r1.Id);

        var byRange = await _store.QueryAsync(new AuditQuery { TenantId = tenant, From = now.AddMinutes(-7), To = now.AddMinutes(-3) });
        byRange.Records.Should().HaveCount(1);
        byRange.Records[0].Id.Should().Be(r2.Id);

        var byToOnly = await _store.QueryAsync(new AuditQuery { TenantId = tenant, To = now.AddMinutes(-7) });
        byToOnly.Records.Should().HaveCount(1);
        byToOnly.Records[0].Id.Should().Be(r1.Id);

        var emptyQuery = await _store.QueryAsync(new AuditQuery { TenantId = "non-existent-tenant" });
        emptyQuery.Records.Should().BeEmpty();
        emptyQuery.HasMore.Should().BeFalse();
        emptyQuery.NextCursorId.Should().BeNull();
    }

    [Fact]
    public void Sqlite_UseSqlite_RegistrationAndValidation()
    {
        var services = new ServiceCollection();
        var builder = services.AddAuditing();

        Assert.Throws<ArgumentNullException>(() => SqliteAuditExtensions.UseSqlite(null!, opt => { }));
        Assert.Throws<ArgumentNullException>(() => builder.UseSqlite(null!));

        services.AddSingleton<IAuditIntegrityProvider, TestAuditIntegrityProvider>();
        services.AddSingleton<HmacAuditIntegrityService>();
        builder.UseSqlite(options =>
        {
            options.ConnectionFactory = () => new SqliteConnection($"Data Source={_dbPath}");
            options.Table = "custom_audit_records";
        });

        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<SqliteAuditStoreOptions>();
        options.Table.Should().Be("custom_audit_records");

        var store = provider.GetService<IAuditStore>();
        store.Should().NotBeNull();
        store.Should().BeOfType<SqliteAuditStore>();

        var verifier = provider.GetService<SqliteAuditIntegrityVerifier>();
        verifier.Should().NotBeNull();
    }

    [Fact]
    public void Sqlite_UseSqlite_DefaultConnectionFactory_ThrowsWhenInvoked()
    {
        var services = new ServiceCollection();
        var builder = services.AddAuditing();
        builder.UseSqlite(options => { /* leave ConnectionFactory unconfigured */ });

        var provider = services.BuildServiceProvider();
        var registeredOptions = provider.GetRequiredService<SqliteAuditStoreOptions>();
        Action act = () => registeredOptions.ConnectionFactory();
        var ex = Assert.Throws<InvalidOperationException>(act);
        ex.Message.Should().Be("SqliteAuditStoreOptions.ConnectionFactory must be configured. Call UseSqlite(options => options.ConnectionFactory = () => new SqliteConnection(...)).");
    }

    [Fact(Timeout = 30000)]
    public async Task Sqlite_WithAlreadyOpenConnection_OperationsSucceed()
    {
        var options = new SqliteAuditStoreOptions
        {
            ConnectionFactory = () =>
            {
                var conn = new SqliteConnection($"Data Source={_dbPath}");
                conn.Open();
                return conn;
            },
            Table = "audit_records"
        };
        var store = new SqliteAuditStore(options);
        var verifier = new SqliteAuditIntegrityVerifier(options, _hmac);

        var r = AuditRecordBuilder.BuildDefault(tenantId: "tenant-open-conn");
        var hash1 = _hmac.ComputeHash(r, null);
        r = r with { IntegrityHash = hash1, PreviousHash = null };

        await store.AppendAsync(r);

        var r2 = AuditRecordBuilder.BuildDefault(tenantId: "tenant-open-conn", resourceId: "res-2");
        var hash2 = _hmac.ComputeHash(r2, hash1);
        r2 = r2 with { IntegrityHash = hash2, PreviousHash = hash1 };

        await store.AppendBatchAsync(new[] { r2 });

        var query = await store.QueryAsync(new AuditQuery { TenantId = "tenant-open-conn" });
        query.Records.Should().HaveCount(2);

        var v = await verifier.VerifyChainAsync("tenant-open-conn", DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        v.IsValid.Should().BeTrue();
    }

    [Fact(Timeout = 30000)]
    public async Task Sqlite_PageSize_BoundariesAndExactMatch()
    {
        var tenant = "tenant-pagesize-boundary";
        var r1 = AuditRecordBuilder.BuildDefault(tenantId: tenant, resourceId: "1");
        var r2 = AuditRecordBuilder.BuildDefault(tenantId: tenant, resourceId: "2");
        await _store.AppendBatchAsync(new[] { r1, r2 });

        // PageSize = 1
        var p1 = await _store.QueryAsync(new AuditQuery { TenantId = tenant, PageSize = 1 });
        p1.Records.Should().HaveCount(1);
        p1.HasMore.Should().BeTrue();

        // PageSize = 2 (exact match)
        var p2 = await _store.QueryAsync(new AuditQuery { TenantId = tenant, PageSize = 2 });
        p2.Records.Should().HaveCount(2);
        p2.HasMore.Should().BeFalse();
        p2.NextCursorId.Should().BeNull();

        // PageSize = 1000 (max valid)
        var p1000 = await _store.QueryAsync(new AuditQuery { TenantId = tenant, PageSize = 1000 });
        p1000.Records.Should().HaveCount(2);
    }

    [Fact(Timeout = 30000)]
    public async Task Sqlite_VerifyChain_SingleRecord_And_Cancellation()
    {
        var tenant = "tenant-single-verify";
        var r = AuditRecordBuilder.BuildDefault(tenantId: tenant);
        var hash = _hmac.ComputeHash(r, null);
        await _store.AppendAsync(r with { IntegrityHash = hash, PreviousHash = null });

        var res = await _verifier.VerifyChainAsync(tenant, DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        res.IsValid.Should().BeTrue();
        res.VerifiedCount.Should().Be(1);

        using var cts = new CancellationTokenSource();
        cts.Cancel();
        Func<Task> cancelVerify = async () => await _verifier.VerifyChainAsync(tenant, DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1), cts.Token);
        await cancelVerify.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact(Timeout = 30000)]
    public async Task Sqlite_EmptyChangesJson_DeserializesToNull()
    {
        var tenant = "tenant-empty-changes";
        var r = AuditRecordBuilder.BuildDefault(tenantId: tenant);
        await _store.AppendAsync(r);

        using (var conn = new SqliteConnection($"Data Source={_dbPath}"))
        {
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"UPDATE audit_records SET changes = '[]' WHERE id = '{r.Id:D}';";
            cmd.ExecuteNonQuery();
        }

        var result = await _store.QueryAsync(new AuditQuery { TenantId = tenant });
        result.Records[0].Changes.Should().BeNull();
    }

    [Fact]
    public void SqliteAuditStoreOptions_DefaultValues()
    {
        var options = new SqliteAuditStoreOptions();
        options.Table.Should().Be("audit_records");
    }

    [Fact(Timeout = 30000)]
    public async Task Sqlite_VerifyChain_SubchainStartingWithNonGenesisRecord_Succeeds()
    {
        var tenant = "tenant-subchain";
        var r = AuditRecordBuilder.BuildDefault(tenantId: tenant);
        var hash = _hmac.ComputeHash(r, "prior-hash-from-older-record");
        await _store.AppendAsync(r with { IntegrityHash = hash, PreviousHash = "prior-hash-from-older-record" });

        var res = await _verifier.VerifyChainAsync(tenant, DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        res.IsValid.Should().BeTrue();
        res.VerifiedCount.Should().Be(1);
    }

    [Fact(Timeout = 30000)]
    public async Task Sqlite_DateTimeRoundTrip_PrecisionAndIso8601Preserved()
    {
        var tenant = "tenant-dt-roundtrip";
        var dt = new DateTimeOffset(2026, 8, 21, 14, 30, 45, 123, TimeSpan.Zero);
        var r = AuditRecordBuilder.BuildDefault(tenantId: tenant) with { OccurredAt = dt };
        await _store.AppendAsync(r);

        var res = await _store.QueryAsync(new AuditQuery { TenantId = tenant, From = dt, To = dt });
        res.Records.Should().HaveCount(1);
        res.Records[0].OccurredAt.Should().Be(dt);
        res.Records[0].OccurredAt.Millisecond.Should().Be(123);
    }

    [Fact(Timeout = 30000)]
    public async Task Sqlite_Query_WhenToIsNull_DoesNotApplyMaxDateFilter()
    {
        var tenant = "tenant-to-null";
        var r = AuditRecordBuilder.BuildDefault(tenantId: tenant) with { OccurredAt = DateTimeOffset.UtcNow.AddDays(10) };
        await _store.AppendAsync(r);

        var res = await _store.QueryAsync(new AuditQuery { TenantId = tenant, To = null });
        res.Records.Should().HaveCount(1);
    }
}
