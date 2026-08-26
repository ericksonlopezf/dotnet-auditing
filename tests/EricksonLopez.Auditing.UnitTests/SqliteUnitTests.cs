// Copyright © Erickson Lopez. MIT License.
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using EricksonLopez.Auditing;
using EricksonLopez.Auditing.Sqlite;
using EricksonLopez.Auditing.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EricksonLopez.Auditing.UnitTests;

public sealed class SqliteUnitTests
{
    private static HmacAuditIntegrityService CreateHmacService() =>
        new(new TestAuditIntegrityProvider());

    private static SqliteAuditStore CreateStore(FakeDbConnection connection, string? table = null)
    {
        var options = new SqliteAuditStoreOptions
        {
            ConnectionFactory = () => connection
        };
        if (table != null)
        {
            options.Table = table;
        }
        return new SqliteAuditStore(options);
    }

    [Fact]
    public void Constructor_NullOptions_Throws()
    {
        Action act = () => _ = new SqliteAuditStore(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task AppendAsync_NullRecord_Throws()
    {
        var store = CreateStore(new FakeDbConnection());
        Func<Task> act = async () => await store.AppendAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task AppendBatchAsync_NullRecords_Throws()
    {
        var store = CreateStore(new FakeDbConnection());
        Func<Task> act = async () => await store.AppendBatchAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task AppendBatchAsync_EmptyList_ReturnsImmediately()
    {
        var conn = new FakeDbConnection();
        var store = CreateStore(conn);
        await store.AppendBatchAsync(Array.Empty<AuditRecord>());
        conn.ExecutedCommands.Should().BeEmpty();
    }

    [Fact]
    public async Task AppendBatchAsync_CrossTenantRecords_ThrowsInvalidOperationException()
    {
        var conn = new FakeDbConnection();
        var store = CreateStore(conn);
        var r1 = AuditRecordBuilder.BuildDefault(tenantId: "tenant-1");
        var r2 = AuditRecordBuilder.BuildDefault(tenantId: "tenant-2");

        Func<Task> act = async () => await store.AppendBatchAsync(new[] { r1, r2 });
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*All records in a batch must belong to the same tenant*");
    }

    [Fact]
    public async Task QueryAsync_NullQuery_Throws()
    {
        var store = CreateStore(new FakeDbConnection());
        Func<Task> act = async () => await store.QueryAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task AppendAsync_WithoutChanges_SerializesNullChanges()
    {
        var conn = new FakeDbConnection();
        var store = CreateStore(conn);
        var record = AuditRecordBuilder.BuildDefault();

        await store.AppendAsync(record);

        conn.ExecutedCommands.Should().HaveCount(1);
        conn.ExecutedCommands[0].Parameters["Changes"].Value.Should().Be(DBNull.Value);
    }

    [Fact]
    public async Task AppendAsync_ExecutesExpectedSqlAndParameters()
    {
        var conn = new FakeDbConnection();
        var store = CreateStore(conn);
        var record = AuditRecordBuilder.Create()
            .WithId(Guid.NewGuid())
            .WithTenant("tenant-a")
            .WithSource("OrderService")
            .WithActor(AuditActorType.User, "user-123", "Alice")
            .WithAction(AuditAction.Create)
            .WithResource("Order", "ord-1", "Customer", "cust-1")
            .WithOutcome(AuditOutcome.Success)
            .WithCorrelationId("corr-1")
            .WithCausationId("cause-1")
            .WithRequestId("req-1")
            .WithIpAddress("127.0.0.1")
            .WithUserAgent("TestAgent")
            .WithErrorCode(null)
            .WithIntegrityHash("hash123")
            .WithPreviousHash("prev123")
            .AddChange("Status", "Draft", "Submitted")
            .AddRedactedChange("SecretField")
            .Build();

        await store.AppendAsync(record);

        conn.ExecutedCommands.Should().HaveCount(1);
        var cmd = conn.ExecutedCommands[0];
        cmd.CommandText.Should().Contain("INSERT INTO audit_records");
        cmd.Parameters["Id"].Value.Should().Be(record.Id.ToString("D"));
        cmd.Parameters["OccurredAt"].Value.Should().Be(record.OccurredAt.ToString("O", CultureInfo.InvariantCulture));
        cmd.Parameters["TenantId"].Value.Should().Be("tenant-a");
        cmd.Parameters["Source"].Value.Should().Be("OrderService");
        cmd.Parameters["ActorType"].Value.Should().Be((byte)AuditActorType.User);
        cmd.Parameters["ActorId"].Value.Should().Be("user-123");
        cmd.Parameters["ActorName"].Value.Should().Be("Alice");
        cmd.Parameters["ActionCode"].Value.Should().Be("Create");
        cmd.Parameters["ResourceType"].Value.Should().Be("Order");
        cmd.Parameters["ResourceId"].Value.Should().Be("ord-1");
        cmd.Parameters["AggregateType"].Value.Should().Be("Customer");
        cmd.Parameters["AggregateId"].Value.Should().Be("cust-1");
        cmd.Parameters["Outcome"].Value.Should().Be((byte)AuditOutcome.Success);
        cmd.Parameters["ErrorCode"].Value.Should().Be(DBNull.Value);
        cmd.Parameters["CorrelationId"].Value.Should().Be("corr-1");
        cmd.Parameters["CausationId"].Value.Should().Be("cause-1");
        cmd.Parameters["RequestId"].Value.Should().Be("req-1");
        cmd.Parameters["IpAddress"].Value.Should().Be("127.0.0.1");
        cmd.Parameters["UserAgent"].Value.Should().Be("TestAgent");
        cmd.Parameters["Changes"].Value.Should().NotBeNull();
        cmd.Parameters["IntegrityHash"].Value.Should().Be("hash123");
        cmd.Parameters["PreviousHash"].Value.Should().Be("prev123");
    }

    [Fact]
    public async Task AppendAsync_NullRecord_ThrowsArgumentNullException()
    {
        var conn = new FakeDbConnection();
        var store = CreateStore(conn);

        Func<Task> act = async () => await store.AppendAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task AppendBatchAsync_EmptyList_DoesNotOpenConnectionOrExecute()
    {
        var conn = new FakeDbConnection();
        var store = CreateStore(conn);

        await store.AppendBatchAsync(Array.Empty<AuditRecord>());

        conn.ExecutedCommands.Should().BeEmpty();
    }

    [Fact]
    public async Task AppendBatchAsync_NullRecords_ThrowsArgumentNullException()
    {
        var conn = new FakeDbConnection();
        var store = CreateStore(conn);

        Func<Task> act = async () => await store.AppendBatchAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task AppendBatchAsync_CrossTenantBatch_ThrowsInvalidOperationException()
    {
        var conn = new FakeDbConnection();
        var store = CreateStore(conn);
        var r1 = AuditRecordBuilder.BuildDefault(tenantId: "tenant-1");
        var r2 = AuditRecordBuilder.BuildDefault(tenantId: "tenant-2");

        Func<Task> act = async () => await store.AppendBatchAsync(new[] { r1, r2 });

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*same tenant*");
    }

    [Fact]
    public async Task AppendBatchAsync_SingleTenant_InsertsAllRecords()
    {
        var conn = new FakeDbConnection();
        var store = CreateStore(conn);
        var r1 = AuditRecordBuilder.BuildDefault(tenantId: "tenant-a", resourceId: "1");
        var r2 = AuditRecordBuilder.BuildDefault(tenantId: "tenant-a", resourceId: "2");

        await store.AppendBatchAsync(new[] { r1, r2 });

        conn.ExecutedCommands.Should().HaveCount(2);
        conn.ExecutedCommands[0].CommandText.Should().Contain("INSERT INTO audit_records");
        conn.ExecutedCommands[1].CommandText.Should().Contain("INSERT INTO audit_records");
    }

    [Fact]
    public async Task QueryAsync_NullQuery_ThrowsArgumentNullException()
    {
        var conn = new FakeDbConnection();
        var store = CreateStore(conn);

        Func<Task> act = async () => await store.QueryAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(1001)]
    public async Task QueryAsync_InvalidPageSize_ThrowsArgumentOutOfRangeException(int pageSize)
    {
        var conn = new FakeDbConnection();
        var store = CreateStore(conn);

        Func<Task> act = async () => await store.QueryAsync(new AuditQuery
        {
            TenantId = "tenant-a",
            PageSize = pageSize
        });

        await act.Should().ThrowAsync<ArgumentOutOfRangeException>()
            .WithMessage("*PageSize must be between 1 and 1000.*");
    }

    [Fact]
    public async Task QueryAsync_BuildsAllFilterConditionsCorrectly()
    {
        var conn = new FakeDbConnection();
        var store = CreateStore(conn);
        var cursorId = Guid.NewGuid();
        var fromDate = DateTimeOffset.UtcNow.AddDays(-7);
        var toDate = DateTimeOffset.UtcNow;

        conn.ReaderQueues.Enqueue(_ => FakeDbDataReaderFactory.Create(Array.Empty<AuditRecord>(), isStringId: true, isStringDate: true));

        var query = new AuditQuery
        {
            TenantId = "tenant-a",
            ActorId = "actor-1",
            ActionCode = "Create",
            ResourceType = "Invoice",
            ResourceId = "inv-001",
            Outcome = AuditOutcome.Failure,
            From = fromDate,
            To = toDate,
            CorrelationId = "corr-xyz",
            AfterRecordId = cursorId,
            PageSize = 25
        };

        var result = await store.QueryAsync(query);

        result.Records.Should().BeEmpty();
        result.HasMore.Should().BeFalse();
        result.NextCursorId.Should().BeNull();

        conn.ExecutedCommands.Should().HaveCount(1);
        var cmd = conn.ExecutedCommands[0];
        cmd.CommandText.Should().Contain("WHERE tenant_id = @TenantId AND occurred_at >= @MinDate");
        cmd.CommandText.Should().Contain("tenant_id = @TenantId");
        cmd.CommandText.Should().Contain("occurred_at >= @MinDate");
        cmd.CommandText.Should().Contain("occurred_at <= @MaxDate");
        cmd.CommandText.Should().Contain("actor_id = @ActorId");
        cmd.CommandText.Should().Contain("action_code = @ActionCode");
        cmd.CommandText.Should().Contain("resource_type = @ResourceType");
        cmd.CommandText.Should().Contain("resource_id = @ResourceId");
        cmd.CommandText.Should().Contain("outcome = @Outcome");
        cmd.CommandText.Should().Contain("correlation_id = @CorrelationId");
        cmd.CommandText.Should().Contain("occurred_at > (SELECT occurred_at FROM audit_records WHERE id = @CursorId)");
        cmd.CommandText.Should().Contain("LIMIT 26");

        cmd.Parameters["TenantId"].Value.Should().Be("tenant-a");
        cmd.Parameters["ActorId"].Value.Should().Be("actor-1");
        cmd.Parameters["ActionCode"].Value.Should().Be("Create");
        cmd.Parameters["ResourceType"].Value.Should().Be("Invoice");
        cmd.Parameters["ResourceId"].Value.Should().Be("inv-001");
        cmd.Parameters["Outcome"].Value.Should().Be((byte)AuditOutcome.Failure);
        cmd.Parameters["MinDate"].Value.Should().Be(fromDate.ToString("O", CultureInfo.InvariantCulture));
        cmd.Parameters["MaxDate"].Value.Should().Be(toDate.ToString("O", CultureInfo.InvariantCulture));
        cmd.Parameters["CorrelationId"].Value.Should().Be("corr-xyz");
        cmd.Parameters["CursorId"].Value.Should().Be(cursorId.ToString("D"));
    }

    [Fact]
    public async Task QueryAsync_Pagination_HasMoreAndNextCursor()
    {
        var conn = new FakeDbConnection();
        var store = CreateStore(conn);
        var r1 = AuditRecordBuilder.BuildDefault(resourceId: "1");
        var r2 = AuditRecordBuilder.BuildDefault(resourceId: "2");
        var r3 = AuditRecordBuilder.BuildDefault(resourceId: "3");

        // Return 3 rows when PageSize is 2 (means hasMore = true)
        conn.ReaderQueues.Enqueue(_ => FakeDbDataReaderFactory.Create(new[] { r1, r2, r3 }, isStringId: true, isStringDate: true));

        var result = await store.QueryAsync(new AuditQuery
        {
            TenantId = "tenant-a",
            PageSize = 2
        });

        result.Records.Should().HaveCount(2);
        result.HasMore.Should().BeTrue();
        result.NextCursorId.Should().Be(r2.Id);
    }

    [Fact]
    public async Task QueryAsync_ReturnsRecordsWithAndWithoutChanges_CorrectlyMapped()
    {
        var conn = new FakeDbConnection();
        var store = CreateStore(conn);
        var r1 = AuditRecordBuilder.Create()
            .WithId(Guid.NewGuid())
            .WithTenant("tenant-a")
            .WithActor(AuditActorType.User, "user-1", "Alice")
            .WithAction(AuditAction.Create)
            .WithResource("Order", "ord-1", "Customer", "cust-1")
            .WithOutcome(AuditOutcome.Success)
            .WithCorrelationId("corr-1")
            .WithCausationId("cause-1")
            .WithRequestId("req-1")
            .WithIpAddress("127.0.0.1")
            .WithUserAgent("TestAgent")
            .WithErrorCode("ERR")
            .WithIntegrityHash("hash-1")
            .WithPreviousHash("prev-1")
            .AddChange("Field1", "old", "new")
            .Build();

        var r2 = AuditRecordBuilder.BuildDefault(tenantId: "tenant-a", resourceId: "2");

        conn.ReaderQueues.Enqueue(_ => FakeDbDataReaderFactory.Create(new[] { r1, r2 }, isStringId: true, isStringDate: true));

        var result = await store.QueryAsync(new AuditQuery
        {
            TenantId = "tenant-a",
            PageSize = 10
        });

        result.Records.Should().HaveCount(2);
        result.HasMore.Should().BeFalse();
        result.NextCursorId.Should().BeNull();
        result.Records[0].Changes.Should().NotBeNull();
        result.Records[0].Changes!.Count.Should().Be(1);
        result.Records[0].Changes![0].Field.Should().Be("Field1");
        result.Records[1].Changes.Should().BeNull();
    }

    [Fact]
    public void SqliteAuditExtensions_UseSqlite_RegistersServices()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IAuditIntegrityProvider, TestAuditIntegrityProvider>();
        services.AddSingleton<HmacAuditIntegrityService>();
        var builder = services.AddAuditing();

        builder.UseSqlite(options =>
        {
            options.ConnectionFactory = () => new FakeDbConnection();
            options.Table = "custom_audit_records";
        });

        var provider = services.BuildServiceProvider();
        provider.GetService<IAuditStore>().Should().BeOfType<SqliteAuditStore>();
        provider.GetService<SqliteAuditStoreOptions>().Should().NotBeNull();
        provider.GetService<SqliteAuditStoreOptions>()!.Table.Should().Be("custom_audit_records");
        provider.GetService<SqliteAuditIntegrityVerifier>().Should().NotBeNull();
    }

    [Fact]
    public void SqliteAuditExtensions_NullGuards()
    {
        IAuditBuilder builder = null!;
        Action act1 = () => builder.UseSqlite(_ => { });
        act1.Should().Throw<ArgumentNullException>();

        var services = new ServiceCollection();
        var validBuilder = services.AddAuditing();
        Action act2 = () => validBuilder.UseSqlite(null!);
        act2.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void SqliteAuditExtensions_MissingConnectionFactory_ThrowsInvalidOperationException()
    {
        var services = new ServiceCollection();
        var builder = services.AddAuditing();

        builder.UseSqlite(options =>
        {
            // Do not override ConnectionFactory
        });

        var provider = services.BuildServiceProvider();
        var opts = provider.GetRequiredService<SqliteAuditStoreOptions>();
        Action act = () => opts.ConnectionFactory();
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*ConnectionFactory*");
    }

    // ── Verifier Tests ────────────────────────────────────────────────────────

    [Fact]
    public void Verifier_Constructor_NullGuards()
    {
        var hmac = CreateHmacService();
        var options = new SqliteAuditStoreOptions { ConnectionFactory = () => new FakeDbConnection() };

        Action act1 = () => _ = new SqliteAuditIntegrityVerifier(null!, hmac);
        act1.Should().Throw<ArgumentNullException>();

        Action act2 = () => _ = new SqliteAuditIntegrityVerifier(options, null!);
        act2.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task Verifier_NullOrEmptyTenant_ThrowsArgumentException(string? tenantId)
    {
        var hmac = CreateHmacService();
        var options = new SqliteAuditStoreOptions { ConnectionFactory = () => new FakeDbConnection() };
        var verifier = new SqliteAuditIntegrityVerifier(options, hmac);

        Func<Task> act = async () => await verifier.VerifyChainAsync(tenantId!, DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task Verifier_EmptyChain_ReturnsValidWithZeroCount()
    {
        var conn = new FakeDbConnection();
        var hmac = CreateHmacService();
        var options = new SqliteAuditStoreOptions { ConnectionFactory = () => conn };
        var verifier = new SqliteAuditIntegrityVerifier(options, hmac);

        conn.ReaderQueues.Enqueue(_ => FakeDbDataReaderFactory.Create(Array.Empty<AuditRecord>(), isStringId: true, isStringDate: true));

        var result = await verifier.VerifyChainAsync("tenant-a", DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow);

        result.IsValid.Should().BeTrue();
        result.VerifiedCount.Should().Be(0);
        result.FirstFailedRecordId.Should().BeNull();
        result.FailureReason.Should().BeNull();
    }

    [Fact]
    public async Task Verifier_ValidChain_ReturnsValid()
    {
        var conn = new FakeDbConnection();
        var hmac = CreateHmacService();
        var options = new SqliteAuditStoreOptions { ConnectionFactory = () => conn };
        var verifier = new SqliteAuditIntegrityVerifier(options, hmac);

        var r1 = AuditRecordBuilder.BuildDefault(tenantId: "tenant-a", resourceId: "1");
        var hash1 = hmac.ComputeHash(r1, null);
        var signed1 = r1 with { IntegrityHash = hash1, PreviousHash = null };

        var r2 = AuditRecordBuilder.BuildDefault(tenantId: "tenant-a", resourceId: "2");
        var hash2 = hmac.ComputeHash(r2, hash1);
        var signed2 = r2 with { IntegrityHash = hash2, PreviousHash = hash1 };

        conn.ReaderQueues.Enqueue(_ => FakeDbDataReaderFactory.Create(new[] { signed1, signed2 }, isStringId: true, isStringDate: true));

        var result = await verifier.VerifyChainAsync("tenant-a", DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow);

        result.IsValid.Should().BeTrue();
        result.VerifiedCount.Should().Be(2);
        result.FirstFailedRecordId.Should().BeNull();
        result.FailureReason.Should().BeNull();
    }

    [Fact]
    public async Task Verifier_BrokenChainLink_ReturnsInvalid()
    {
        var conn = new FakeDbConnection();
        var hmac = CreateHmacService();
        var options = new SqliteAuditStoreOptions { ConnectionFactory = () => conn };
        var verifier = new SqliteAuditIntegrityVerifier(options, hmac);

        var r1 = AuditRecordBuilder.BuildDefault(tenantId: "tenant-a", resourceId: "1");
        var hash1 = hmac.ComputeHash(r1, null);
        var signed1 = r1 with { IntegrityHash = hash1, PreviousHash = null };

        var r2 = AuditRecordBuilder.BuildDefault(tenantId: "tenant-a", resourceId: "2");
        // previous_hash does NOT match hash1
        var signed2 = r2 with { IntegrityHash = "invalid_hash", PreviousHash = "wrong_prev_hash" };

        conn.ReaderQueues.Enqueue(_ => FakeDbDataReaderFactory.Create(new[] { signed1, signed2 }, isStringId: true, isStringDate: true));

        var result = await verifier.VerifyChainAsync("tenant-a", DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow);

        result.IsValid.Should().BeFalse();
        result.VerifiedCount.Should().Be(2);
        result.FirstFailedRecordId.Should().Be(signed2.Id);
        result.FailureReason.Should().Contain("Chain break");
    }

    [Fact]
    public async Task Verifier_TamperedContent_ReturnsInvalid()
    {
        var conn = new FakeDbConnection();
        var hmac = CreateHmacService();
        var options = new SqliteAuditStoreOptions { ConnectionFactory = () => conn };
        var verifier = new SqliteAuditIntegrityVerifier(options, hmac);

        var r1 = AuditRecordBuilder.BuildDefault(tenantId: "tenant-a", resourceId: "1");
        // Compute hash on original, then alter action code
        var hash1 = hmac.ComputeHash(r1, null);
        var tampered = r1 with { Action = AuditAction.Delete, IntegrityHash = hash1, PreviousHash = null };

        conn.ReaderQueues.Enqueue(_ => FakeDbDataReaderFactory.Create(new[] { tampered }, isStringId: true, isStringDate: true));

        var result = await verifier.VerifyChainAsync("tenant-a", DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow);

        result.IsValid.Should().BeFalse();
        result.VerifiedCount.Should().Be(1);
        result.FirstFailedRecordId.Should().Be(tampered.Id);
        result.FailureReason.Should().Contain("tampered with");
    }

    [Fact]
    public void Extensions_UseSqlite_RegistersServices()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IAuditIntegrityProvider, TestAuditIntegrityProvider>();
        services.AddSingleton<HmacAuditIntegrityService>();
        var builder = services.AddAuditing();
        builder.UseSqlite(options =>
        {
            options.ConnectionFactory = () => new FakeDbConnection();
            options.Table = "custom_sqlite_audit";
        });

        var provider = services.BuildServiceProvider();
        provider.GetService<SqliteAuditStoreOptions>().Should().NotBeNull();
        provider.GetService<SqliteAuditStoreOptions>()!.Table.Should().Be("custom_sqlite_audit");
        provider.GetService<IAuditStore>().Should().BeOfType<SqliteAuditStore>();
        provider.GetService<SqliteAuditIntegrityVerifier>().Should().NotBeNull();
    }

    [Fact]
    public void Extensions_UseSqlite_NullGuards()
    {
        IAuditBuilder nullBuilder = null!;
        Action act1 = () => nullBuilder.UseSqlite(_ => { });
        act1.Should().Throw<ArgumentNullException>();

        var services = new ServiceCollection();
        var builder = services.AddAuditing();
        Action act2 = () => builder.UseSqlite(null!);
        act2.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Extensions_UseSqlite_UnconfiguredConnectionFactory_Throws()
    {
        var services = new ServiceCollection();
        var builder = services.AddAuditing();
        builder.UseSqlite(_ => { });

        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<SqliteAuditStoreOptions>();
        Action act = () => options.ConnectionFactory();
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*SqliteAuditStoreOptions.ConnectionFactory must be configured*")
            .WithMessage("*Call UseSqlite(options => options.ConnectionFactory = () => new SqliteConnection(...)).*");
    }

    [Fact]
    public async Task AppendBatchAsync_CrossTenantRecords_ThrowsDetailedException()
    {
        var conn = new FakeDbConnection();
        var store = CreateStore(conn);
        var r1 = AuditRecordBuilder.BuildDefault(tenantId: "tenant-1");
        var r2 = AuditRecordBuilder.BuildDefault(tenantId: "tenant-2");

        Func<Task> act = async () => await store.AppendBatchAsync(new[] { r1, r2 });
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Split cross-tenant records into separate batch operations.*");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(1000)]
    public async Task QueryAsync_BoundaryPageSize_DoesNotThrow(int pageSize)
    {
        var conn = new FakeDbConnection();
        conn.ReaderQueues.Enqueue(_ => FakeDbDataReaderFactory.Create(Array.Empty<AuditRecord>(), isStringId: true, isStringDate: true));
        var store = CreateStore(conn);
        var query = new AuditQuery { TenantId = "tenant-1", PageSize = pageSize };

        var result = await store.QueryAsync(query);
        result.Records.Should().BeEmpty();
    }

    [Fact]
    public async Task QueryAsync_ExactPageSizeMatchesListCount_HasMoreIsFalse()
    {
        var conn = new FakeDbConnection();
        var r1 = AuditRecordBuilder.BuildDefault(tenantId: "tenant-exact");
        var r2 = AuditRecordBuilder.BuildDefault(tenantId: "tenant-exact");
        conn.ReaderQueues.Enqueue(_ => FakeDbDataReaderFactory.Create(new[] { r1, r2 }, isStringId: true, isStringDate: true));

        var store = CreateStore(conn);
        var result = await store.QueryAsync(new AuditQuery { TenantId = "tenant-exact", PageSize = 2 });

        result.Records.Should().HaveCount(2);
        result.HasMore.Should().BeFalse();
        result.NextCursorId.Should().BeNull();
    }

    [Fact]
    public async Task QueryAsync_WithAfterRecordId_FormatsCursorIdParameter()
    {
        var conn = new FakeDbConnection();
        conn.ReaderQueues.Enqueue(_ => FakeDbDataReaderFactory.Create(Array.Empty<AuditRecord>(), isStringId: true, isStringDate: true));

        var store = CreateStore(conn);
        var cursorId = Guid.NewGuid();
        await store.QueryAsync(new AuditQuery { TenantId = "tenant-cursor", AfterRecordId = cursorId, PageSize = 10 });

        conn.ExecutedCommands.Should().HaveCount(1);
        conn.ExecutedCommands[0].Parameters["CursorId"].Value.Should().Be(cursorId.ToString());
    }

    [Fact]
    public async Task AppendAsync_Parameters_FormattingAndChanges()
    {
        var conn = new FakeDbConnection();
        var store = CreateStore(conn);
        var record = AuditRecordBuilder.Create()
            .WithTenant("tenant-p")
            .WithActor(AuditActorType.User, "usr-1", "Alice")
            .WithAction(AuditAction.Create)
            .WithResource("Order", "ord-1")
            .WithOutcome(AuditOutcome.Success)
            .AddChange("Field1", "old", "new")
            .Build();

        await store.AppendAsync(record);

        conn.ExecutedCommands.Should().HaveCount(1);
        conn.ExecutedCommands[0].Parameters["Id"].Value.Should().Be(record.Id.ToString());
        conn.ExecutedCommands[0].Parameters["Changes"].Value.Should().BeOfType<string>().Which.Should().Contain("Field1");
    }

    [Fact]
    public async Task QueryAsync_WithEmptyJsonArrayChanges_ReturnsNullChanges()
    {
        var conn = new FakeDbConnection();
        var r = AuditRecordBuilder.BuildDefault(tenantId: "tenant-changes");
        var rows = new List<object?[]>
        {
            new object?[]
            {
                r.Id.ToString("D"),
                r.OccurredAt.ToString("O"),
                r.Context.TenantId,
                r.Context.Source,
                (byte)r.Actor.Type,
                r.Actor.Id,
                r.Actor.DisplayName,
                r.Action.Code,
                r.Resource.Type,
                r.Resource.Id,
                r.Resource.AggregateType,
                r.Resource.AggregateId,
                (byte)r.Outcome,
                r.ErrorCode,
                r.Context.CorrelationId,
                r.Context.CausationId,
                r.Context.RequestId,
                r.Context.IpAddress,
                r.Context.UserAgent,
                "[]",
                r.IntegrityHash,
                r.PreviousHash
            }
        };
        conn.ReaderQueues.Enqueue(_ => FakeDbDataReaderFactory.CreateRaw(FakeDbDataReaderFactory.StandardColumns, rows));

        var store = CreateStore(conn);
        var result = await store.QueryAsync(new AuditQuery { TenantId = "tenant-changes" });

        result.Records.Should().HaveCount(1);
        result.Records[0].Changes.Should().BeNull();
    }

    [Fact]
    public async Task Verifier_CancelledToken_ThrowsOperationCanceledException()
    {
        var hmac = CreateHmacService();
        var conn = new FakeDbConnection();
        var r = AuditRecordBuilder.BuildDefault(tenantId: "tenant-1");
        conn.ReaderQueues.Enqueue(_ => FakeDbDataReaderFactory.Create(new[] { r }, isStringId: true, isStringDate: true));
        var verifier = new SqliteAuditIntegrityVerifier(new SqliteAuditStoreOptions { ConnectionFactory = () => conn }, hmac);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Func<Task> act = async () => await verifier.VerifyChainAsync("tenant-1", DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow, cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task Verifier_DateParameters_FormattedAsIso8601()
    {
        var hmac = CreateHmacService();
        var conn = new FakeDbConnection();
        conn.ReaderQueues.Enqueue(_ => FakeDbDataReaderFactory.Create(Array.Empty<AuditRecord>(), isStringId: true, isStringDate: true));
        var verifier = new SqliteAuditIntegrityVerifier(new SqliteAuditStoreOptions { ConnectionFactory = () => conn }, hmac);

        var from = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
        var to = new DateTimeOffset(2026, 1, 2, 12, 0, 0, TimeSpan.Zero);

        var result = await verifier.VerifyChainAsync("tenant-iso", from, to);
        result.IsValid.Should().BeTrue();

        conn.ExecutedCommands.Should().HaveCount(1);
        conn.ExecutedCommands[0].Parameters["From"].Value.Should().Be(from.ToString("O", CultureInfo.InvariantCulture));
        conn.ExecutedCommands[0].Parameters["To"].Value.Should().Be(to.ToString("O", CultureInfo.InvariantCulture));
    }

    [Fact]
    public async Task Verifier_SingleRecordWithPreviousHash_IsValid()
    {
        var hmac = CreateHmacService();
        var conn = new FakeDbConnection();
        var r = AuditRecordBuilder.BuildDefault(tenantId: "tenant-genesis");
        var hash = hmac.ComputeHash(r, "genesis-previous-hash");
        var signed = r with { IntegrityHash = hash, PreviousHash = "genesis-previous-hash" };

        conn.ReaderQueues.Enqueue(_ => FakeDbDataReaderFactory.Create(new[] { signed }, isStringId: true, isStringDate: true));
        var verifier = new SqliteAuditIntegrityVerifier(new SqliteAuditStoreOptions { ConnectionFactory = () => conn }, hmac);

        var result = await verifier.VerifyChainAsync("tenant-genesis", DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Store_And_Verifier_WithCustomTable_UsesTable()
    {
        var fakeConn = new FakeDbConnection();
        var store = new SqliteAuditStore(new SqliteAuditStoreOptions
        {
            ConnectionFactory = () => fakeConn,
            Table = "my_custom_table"
        });
        var hmac = CreateHmacService();
        var verifier = new SqliteAuditIntegrityVerifier(new SqliteAuditStoreOptions
        {
            ConnectionFactory = () => fakeConn,
            Table = "my_custom_table"
        }, hmac);

        var r = AuditRecordBuilder.BuildDefault(tenantId: "tenant-table");
        await store.AppendAsync(r);

        fakeConn.ExecutedCommands.Should().Contain(c => c.CommandText.Contains("INSERT INTO my_custom_table ("));

        var hash = hmac.ComputeHash(r, null);
        var signed = r with { IntegrityHash = hash, PreviousHash = null };
        fakeConn.ReaderQueues.Enqueue(_ => FakeDbDataReaderFactory.Create(new[] { signed }, isStringId: true, isStringDate: true));
        var result = await verifier.VerifyChainAsync("tenant-table", DateTimeOffset.UtcNow.AddHours(-1), DateTimeOffset.UtcNow);
        result.IsValid.Should().BeTrue();

        fakeConn.ExecutedCommands.Should().Contain(c => c.CommandText.Contains("FROM my_custom_table"));
    }

    [Fact]
    public async Task Store_And_Verifier_WithClosedAndOpenConnections_BehaveCorrectly()
    {
        var hmac = CreateHmacService();
        var closedConn = new FakeDbConnection();
        closedConn.Close();
        var store = new SqliteAuditStore(new SqliteAuditStoreOptions { ConnectionFactory = () => closedConn });
        var verifier = new SqliteAuditIntegrityVerifier(new SqliteAuditStoreOptions { ConnectionFactory = () => closedConn }, hmac);

        var r = AuditRecordBuilder.BuildDefault(tenantId: "tenant-conn");
        await store.AppendAsync(r);

        var hash = hmac.ComputeHash(r, null);
        var signed = r with { IntegrityHash = hash, PreviousHash = null };

        closedConn.Close();
        closedConn.ReaderQueues.Enqueue(_ => FakeDbDataReaderFactory.Create(new[] { signed }, isStringId: true, isStringDate: true));
        var verRes = await verifier.VerifyChainAsync("tenant-conn", DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow);
        verRes.IsValid.Should().BeTrue();

        var openConn = new FakeDbConnection();
        openConn.Open();
        var storeOpen = new SqliteAuditStore(new SqliteAuditStoreOptions { ConnectionFactory = () => openConn });
        var verifierOpen = new SqliteAuditIntegrityVerifier(new SqliteAuditStoreOptions { ConnectionFactory = () => openConn }, hmac);
        await storeOpen.AppendAsync(r);

        openConn.ReaderQueues.Enqueue(_ => FakeDbDataReaderFactory.Create(new[] { signed }, isStringId: true, isStringDate: true));
        var verOpenRes = await verifierOpen.VerifyChainAsync("tenant-conn", DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow);
        verOpenRes.IsValid.Should().BeTrue();
    }
}
