// Copyright © Erickson Lopez. MIT License.
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using EricksonLopez.Auditing;
using EricksonLopez.Auditing.PostgreSql;
using EricksonLopez.Auditing.Testing;
using EricksonLopez.Auditing.Tests.Common;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EricksonLopez.Auditing.PostgreSql.Tests;

public sealed class PostgreSqlUnitTests
{
    private readonly HmacAuditIntegrityService _hmac = new(new TestAuditIntegrityProvider(), new HmacSha256AuditHashAlgorithm());

    [Fact]
    public void Options_DefaultValues()
    {
        var options = new PostgreSqlAuditStoreOptions();
        options.Schema.Should().Be("audit");
        options.Table.Should().Be("records");
    }

    [Fact]
    public void Options_SchemaAndTable_Validation()
    {
        var options = new PostgreSqlAuditStoreOptions();
        options.Schema = "custom_schema";
        options.Schema.Should().Be("custom_schema");

        Action actNullSchema = () => options.Schema = null!;
        actNullSchema.Should().Throw<ArgumentNullException>()
            .WithParameterName("value");

        Action actEmptySchema = () => options.Schema = "";
        actEmptySchema.Should().Throw<ArgumentException>()
            .WithMessage("*The value cannot be an empty string*")
            .WithParameterName("value");

        Action actWsSchema = () => options.Schema = "   ";
        actWsSchema.Should().Throw<ArgumentException>()
            .WithMessage("*The value cannot be an empty string or composed entirely of whitespace*")
            .WithParameterName("value");

        Action actInvalidSchema = () => options.Schema = "invalid-schema!";
        actInvalidSchema.Should().Throw<ArgumentException>()
            .WithParameterName("value");

        options.Table = "custom_table";
        options.Table.Should().Be("custom_table");

        Action actNullTable = () => options.Table = null!;
        actNullTable.Should().Throw<ArgumentNullException>()
            .WithParameterName("value");

        Action actEmptyTable = () => options.Table = "";
        actEmptyTable.Should().Throw<ArgumentException>()
            .WithMessage("*The value cannot be an empty string*")
            .WithParameterName("value");

        Action actWsTable = () => options.Table = "   ";
        actWsTable.Should().Throw<ArgumentException>()
            .WithMessage("*The value cannot be an empty string or composed entirely of whitespace*")
            .WithParameterName("value");

        Action actInvalidTable = () => options.Table = "invalid-table!";
        actInvalidTable.Should().Throw<ArgumentException>()
            .WithParameterName("value");
    }

    [Fact]
    public void Extensions_NullArguments_Throw()
    {
        var services = new ServiceCollection();
        var builder = services.AddAuditing();

        Assert.Throws<ArgumentNullException>(() => PostgreSqlAuditExtensions.UsePostgreSql(null!, opt => { }));
        Assert.Throws<ArgumentNullException>(() => builder.UsePostgreSql(null!));
    }

    [Fact]
    public void Extensions_UnconfiguredConnectionFactory_ThrowsOnInvocation()
    {
        var services = new ServiceCollection();
        var builder = services.AddAuditing();
        builder.UsePostgreSql(opt => { /* leave ConnectionFactory unconfigured */ });

        var sp = services.BuildServiceProvider();
        var options = sp.GetRequiredService<PostgreSqlAuditStoreOptions>();
        Action act = () => options.ConnectionFactory();
        var ex = Assert.Throws<InvalidOperationException>(act);
        ex.Message.Should().Be("PostgreSqlAuditStoreOptions.ConnectionFactory must be configured. Call UsePostgreSql(options => options.ConnectionFactory = () => new NpgsqlConnection(...)).");
    }

    [Fact]
    public void Extensions_RegistersServicesCorrectly()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IAuditIntegrityProvider, TestAuditIntegrityProvider>();
        services.AddSingleton<IAuditHashAlgorithm, HmacSha256AuditHashAlgorithm>();
        services.AddSingleton<HmacAuditIntegrityService>();
        var builder = services.AddAuditing();

        builder.UsePostgreSql(options =>
        {
            options.ConnectionFactory = () => new FakeDbConnection();
            options.Schema = "custom_schema";
            options.Table = "custom_table";
        });

        var sp = services.BuildServiceProvider();
        var options = sp.GetRequiredService<PostgreSqlAuditStoreOptions>();
        options.Schema.Should().Be("custom_schema");
        options.Table.Should().Be("custom_table");

        var store = sp.GetService<IAuditStore>();
        store.Should().NotBeNull();
        store.Should().BeOfType<PostgreSqlAuditStore>();

        var verifier = sp.GetService<PostgreSqlAuditIntegrityVerifier>();
        verifier.Should().NotBeNull();
    }

    [Fact]
    public void Store_Constructor_NullOptions_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new PostgreSqlAuditStore(null!));
    }

    [Fact]
    public async Task Store_AppendAsync_NullRecord_Throws()
    {
        var fakeConn = new FakeDbConnection();
        var store = new PostgreSqlAuditStore(new PostgreSqlAuditStoreOptions { ConnectionFactory = () => fakeConn });

        Func<Task> act = async () => await store.AppendAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task Store_AppendAsync_AllFieldsPopulated_AllParametersMatched()
    {
        var fakeConn = new FakeDbConnection();
        var store = new PostgreSqlAuditStore(new PostgreSqlAuditStoreOptions
        {
            ConnectionFactory = () => fakeConn,
            Schema = "my_audit",
            Table = "my_records"
        });

        var now = DateTimeOffset.UtcNow;
        var id = Guid.NewGuid();
        var record = new AuditRecord
        {
            Id = id,
            OccurredAt = now,
            Actor = new AuditActor(AuditActorType.User, "usr-1", "User One"),
            Action = new AuditAction("Action1"),
            Resource = new AuditResource("ResType", "ResId", "AggType", "AggId"),
            Outcome = AuditOutcome.Failure,
            ErrorCode = "ERR_403",
            Context = new AuditContext(
                TenantId: "tenant-1",
                Source: "Src-1",
                CorrelationId: "corr-1",
                CausationId: "caus-1",
                RequestId: "req-1",
                IpAddress: "10.0.0.1",
                UserAgent: "Browser/1.0"),
            Changes = new[] { new AuditChange("F1", "O", "N", false) },
            IntegrityHash = "hash-1",
            PreviousHash = "prev-0"
        };

        await store.AppendAsync(record);

        fakeConn.ExecutedCommands.Should().HaveCount(2);

        // 1. RLS command
        var rlsCmd = fakeConn.ExecutedCommands[0];
        rlsCmd.CommandText.Should().Contain("set_config('audit.tenant_id'");
        rlsCmd.Parameters["TenantId"].Value.Should().Be("tenant-1");

        // 2. Insert command
        var insertCmd = fakeConn.ExecutedCommands[1];
        insertCmd.CommandText.Should().Contain("INSERT INTO my_audit.my_records");
        insertCmd.Parameters["Id"].Value.Should().Be(id);
        insertCmd.Parameters["OccurredAt"].Value.Should().Be(now);
        insertCmd.Parameters["TenantId"].Value.Should().Be("tenant-1");
        insertCmd.Parameters["Source"].Value.Should().Be("Src-1");
        insertCmd.Parameters["ActorType"].Value.Should().Be((byte)AuditActorType.User);
        insertCmd.Parameters["ActorId"].Value.Should().Be("usr-1");
        insertCmd.Parameters["ActorName"].Value.Should().Be("User One");
        insertCmd.Parameters["ActionCode"].Value.Should().Be("Action1");
        insertCmd.Parameters["ResourceType"].Value.Should().Be("ResType");
        insertCmd.Parameters["ResourceId"].Value.Should().Be("ResId");
        insertCmd.Parameters["AggregateType"].Value.Should().Be("AggType");
        insertCmd.Parameters["AggregateId"].Value.Should().Be("AggId");
        insertCmd.Parameters["Outcome"].Value.Should().Be((byte)AuditOutcome.Failure);
        insertCmd.Parameters["ErrorCode"].Value.Should().Be("ERR_403");
        insertCmd.Parameters["CorrelationId"].Value.Should().Be("corr-1");
        insertCmd.Parameters["CausationId"].Value.Should().Be("caus-1");
        insertCmd.Parameters["RequestId"].Value.Should().Be("req-1");
        insertCmd.Parameters["IpAddress"].Value.Should().Be("10.0.0.1");
        insertCmd.Parameters["UserAgent"].Value.Should().Be("Browser/1.0");
        var changesStr = (string)insertCmd.Parameters["Changes"].Value!;
        changesStr.Should().Contain("F1");
        changesStr.Should().Contain("O");
        changesStr.Should().Contain("N");
        insertCmd.Parameters["IntegrityHash"].Value.Should().Be("hash-1");
        insertCmd.Parameters["PreviousHash"].Value.Should().Be("prev-0");
    }

    [Fact]
    public async Task Store_AppendBatchAsync_NullRecords_Throws()
    {
        var fakeConn = new FakeDbConnection();
        var store = new PostgreSqlAuditStore(new PostgreSqlAuditStoreOptions { ConnectionFactory = () => fakeConn });

        Func<Task> act = async () => await store.AppendBatchAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task Store_AppendBatchAsync_EmptyRecords_DoesNothing()
    {
        var fakeConn = new FakeDbConnection();
        var store = new PostgreSqlAuditStore(new PostgreSqlAuditStoreOptions { ConnectionFactory = () => fakeConn });

        await store.AppendBatchAsync(Array.Empty<AuditRecord>());
        fakeConn.ExecutedCommands.Should().BeEmpty();
    }

    [Fact]
    public async Task Store_AppendBatchAsync_DifferentTenants_Throws()
    {
        var fakeConn = new FakeDbConnection();
        var store = new PostgreSqlAuditStore(new PostgreSqlAuditStoreOptions { ConnectionFactory = () => fakeConn });

        var records = new[]
        {
            AuditRecordBuilder.BuildDefault(tenantId: "tenant-a"),
            AuditRecordBuilder.BuildDefault(tenantId: "tenant-b")
        };

        Func<Task> act = async () => await store.AppendBatchAsync(records);
        var ex = await act.Should().ThrowAsync<InvalidOperationException>();
        ex.Which.Message.Should().Be("All records in a batch must belong to the same tenant. Split cross-tenant records into separate batch operations.");
    }

    [Fact]
    public async Task Store_AppendBatchAsync_ValidRecords_ExecutesRlsAndInsert()
    {
        var fakeConn = new FakeDbConnection();
        var store = new PostgreSqlAuditStore(new PostgreSqlAuditStoreOptions { ConnectionFactory = () => fakeConn });

        var records = new[]
        {
            AuditRecordBuilder.BuildDefault(tenantId: "tenant-batch", resourceId: "res-1"),
            AuditRecordBuilder.BuildDefault(tenantId: "tenant-batch", resourceId: "res-2")
        };

        await store.AppendBatchAsync(records);

        fakeConn.ExecutedCommands.Should().HaveCount(3);
        fakeConn.ExecutedCommands[0].CommandText.Should().Contain("set_config('audit.tenant_id'");
        fakeConn.ExecutedCommands[1].CommandText.Should().Contain("INSERT INTO audit.records");
        fakeConn.ExecutedCommands[2].CommandText.Should().Contain("INSERT INTO audit.records");
    }

    [Fact]
    public async Task Store_QueryAsync_ValidationAndEdgeCases()
    {
        var fakeConn = new FakeDbConnection();
        var store = new PostgreSqlAuditStore(new PostgreSqlAuditStoreOptions { ConnectionFactory = () => fakeConn });

        Func<Task> nullQuery = async () => await store.QueryAsync(null!);
        await nullQuery.Should().ThrowAsync<ArgumentNullException>();

        Func<Task> pageZero = async () => await store.QueryAsync(new AuditQuery { TenantId = "t", PageSize = 0 });
        await pageZero.Should().ThrowAsync<ArgumentOutOfRangeException>();

        Func<Task> pageTooLarge = async () => await store.QueryAsync(new AuditQuery { TenantId = "t", PageSize = 1001 });
        await pageTooLarge.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task Store_QueryAsync_AllFilters_GeneratesExpectedSqlAndParameters()
    {
        var fakeConn = new FakeDbConnection();
        var store = new PostgreSqlAuditStore(new PostgreSqlAuditStoreOptions
        {
            ConnectionFactory = () => fakeConn,
            Schema = "sec_audit",
            Table = "events"
        });

        var now = DateTimeOffset.UtcNow;
        var cursorId = Guid.NewGuid();

        var query = new AuditQuery
        {
            TenantId = "tenant-filter",
            From = now.AddHours(-2),
            To = now,
            ActorId = "actor-123",
            ActionCode = "Update",
            ResourceType = "Document",
            ResourceId = "doc-99",
            Outcome = AuditOutcome.Failure,
            CorrelationId = "corr-555",
            ContinuationToken = AuditCursorToken.Create(System.DateTimeOffset.UtcNow, cursorId),
            PageSize = 50
        };

        var result = await store.QueryAsync(query);

        fakeConn.ExecutedCommands.Should().HaveCount(2);

        var rlsCmd = fakeConn.ExecutedCommands[0];
        rlsCmd.Parameters["TenantId"].Value.Should().Be("tenant-filter");

        var queryCmd = fakeConn.ExecutedCommands[1];
        queryCmd.CommandText.Should().Contain("FROM sec_audit.events");
        queryCmd.CommandText.Should().Contain("WHERE tenant_id = @TenantId AND occurred_at >= @MinDate");
        queryCmd.CommandText.Should().Contain("occurred_at <= @MaxDate");
        queryCmd.CommandText.Should().Contain("actor_id = @ActorId");
        queryCmd.CommandText.Should().Contain("action_code = @ActionCode");
        queryCmd.CommandText.Should().Contain("resource_type = @ResourceType");
        queryCmd.CommandText.Should().Contain("resource_id = @ResourceId");
        queryCmd.CommandText.Should().Contain("outcome = @Outcome");
        queryCmd.CommandText.Should().Contain("correlation_id = @CorrelationId");
        queryCmd.CommandText.Should().Contain("(occurred_at, id) > (");
        queryCmd.CommandText.Should().Contain("LIMIT 51");

        queryCmd.Parameters["TenantId"].Value.Should().Be("tenant-filter");
        queryCmd.Parameters["MinDate"].Value.Should().Be(now.AddHours(-2).UtcDateTime);
        queryCmd.Parameters["MaxDate"].Value.Should().Be(now.UtcDateTime);
        queryCmd.Parameters["ActorId"].Value.Should().Be("actor-123");
        queryCmd.Parameters["ActionCode"].Value.Should().Be("Update");
        queryCmd.Parameters["ResourceType"].Value.Should().Be("Document");
        queryCmd.Parameters["ResourceId"].Value.Should().Be("doc-99");
        queryCmd.Parameters["Outcome"].Value.Should().Be((byte)AuditOutcome.Failure);
        queryCmd.Parameters["CorrelationId"].Value.Should().Be("corr-555");
        queryCmd.Parameters["CursorId"].Value.Should().Be(cursorId);

        result.Records.Should().BeEmpty();
        result.HasMore.Should().BeFalse();
        result.NextPageToken.Should().BeNull();
    }

    [Fact]
    public async Task Store_QueryAsync_EmptyChangesArray_DeserializesToNull()
    {
        var fakeConn = new FakeDbConnection();
        var store = new PostgreSqlAuditStore(new PostgreSqlAuditStoreOptions { ConnectionFactory = () => fakeConn });

        var tenant = "tenant-empty-changes";
        var id = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        fakeConn.ReaderQueues.Enqueue(_ => FakeDbDataReaderFactory.CreateRaw(
            FakeDbDataReaderFactory.StandardColumns,
            new List<object?[]>
            {
                new object?[] { id, now.UtcDateTime, tenant, "Src", (byte)1, "act1", "Actor", "Act", "Res", "1", null, null, (byte)1, null, null, null, null, null, null, "[]", "hash", null }
            }));

        var result = await store.QueryAsync(new AuditQuery { TenantId = tenant });

        result.Records.Should().HaveCount(1);
        result.Records[0].Changes.Should().BeNull();
    }

    [Fact]
    public async Task Store_QueryAsync_RowMappingAndPagination()
    {
        var fakeConn = new FakeDbConnection();
        var store = new PostgreSqlAuditStore(new PostgreSqlAuditStoreOptions { ConnectionFactory = () => fakeConn });

        var r1Id = Guid.NewGuid();
        var r2Id = Guid.NewGuid();
        var r3Id = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var r1 = AuditRecordBuilder.Create()
            .WithId(r1Id)
            .WithOccurredAt(now.AddMinutes(-2))
            .WithTenant("tenant-rows")
            .WithSource("OrderService")
            .WithActor(AuditActorType.User, "u1", "User 1")
            .WithAction("Create")
            .WithResource("Order", "o1", "Agg", "agg1")
            .WithOutcome(AuditOutcome.Success)
            .WithCorrelationId("c1")
            .WithCausationId("ca1")
            .WithRequestId("req1")
            .WithIpAddress("127.0.0.1")
            .WithUserAgent("Browser")
            .WithChanges(new[]
            {
                new AuditChange("Status", "Pending", "Approved", false),
                AuditChange.Redacted("PIN")
            })
            .WithIntegrityHash("hash1")
            .Build();

        var r2 = AuditRecordBuilder.Create()
            .WithId(r2Id)
            .WithOccurredAt(now.AddMinutes(-1))
            .WithTenant("tenant-rows")
            .WithSource("OrderService")
            .WithActor(AuditActorType.Service, "s1", null)
            .WithAction("Delete")
            .WithResource("Order", "o2")
            .WithOutcome(AuditOutcome.Failure)
            .WithErrorCode("ERR")
            .WithIntegrityHash("hash2")
            .WithPreviousHash("hash1")
            .Build();

        var r3 = AuditRecordBuilder.Create()
            .WithId(r3Id)
            .WithOccurredAt(now)
            .WithTenant("tenant-rows")
            .WithSource("OrderService")
            .WithActor(AuditActorType.User, "u2", "User 2")
            .WithAction("Update")
            .WithResource("Order", "o3")
            .WithOutcome(AuditOutcome.Success)
            .WithIntegrityHash("hash3")
            .WithPreviousHash("hash2")
            .Build();

        fakeConn.ReaderQueues.Enqueue(_ => FakeDbDataReaderFactory.Create(r1, r2, r3));

        var queryResult = await store.QueryAsync(new AuditQuery
        {
            TenantId = "tenant-rows",
            PageSize = 2
        });

        queryResult.Records.Should().HaveCount(2);
        queryResult.HasMore.Should().BeTrue();
        EricksonLopez.Auditing.AuditCursorToken.TryParse(queryResult.NextPageToken, out _, out var parsedId).Should().BeTrue(); parsedId.Should().Be(r2Id);

        var first = queryResult.Records[0];
        first.Id.Should().Be(r1Id);
        first.Actor.Type.Should().Be(AuditActorType.User);
        first.Actor.Id.Should().Be("u1");
        first.Actor.DisplayName.Should().Be("User 1");
        first.Changes.Should().NotBeNull();
        first.Changes!.Count.Should().Be(2);
        first.Changes[0].Field.Should().Be("Status");
        first.Changes[0].OldValue.Should().Be("Pending");
        first.Changes[0].NewValue.Should().Be("Approved");
        first.Changes[0].IsRedacted.Should().BeFalse();
        first.Changes[1].Field.Should().Be("PIN");
        first.Changes[1].IsRedacted.Should().BeTrue();

        var second = queryResult.Records[1];
        second.Id.Should().Be(r2Id);
        second.ErrorCode.Should().Be("ERR");
        second.Outcome.Should().Be(AuditOutcome.Failure);
        second.Changes.Should().BeNull();
    }

    [Fact]
    public void Verifier_Constructor_NullArguments_Throw()
    {
        var options = new PostgreSqlAuditStoreOptions { ConnectionFactory = () => new FakeDbConnection() };
        Assert.Throws<ArgumentNullException>(() => new PostgreSqlAuditIntegrityVerifier(null!, _hmac));
        Assert.Throws<ArgumentNullException>(() => new PostgreSqlAuditIntegrityVerifier(options, null!));
    }

    [Fact]
    public async Task Verifier_VerifyChainAsync_NullOrEmptyTenant_Throws()
    {
        var fakeConn = new FakeDbConnection();
        var verifier = new PostgreSqlAuditIntegrityVerifier(new PostgreSqlAuditStoreOptions { ConnectionFactory = () => fakeConn }, _hmac);

        Func<Task> nullTenant = async () => await verifier.VerifyChainAsync(null!, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        await nullTenant.Should().ThrowAsync<ArgumentException>();

        Func<Task> emptyTenant = async () => await verifier.VerifyChainAsync("", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        await emptyTenant.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task Verifier_VerifyChainAsync_ValidChain_Succeeds()
    {
        var fakeConn = new FakeDbConnection();
        var verifier = new PostgreSqlAuditIntegrityVerifier(new PostgreSqlAuditStoreOptions { ConnectionFactory = () => fakeConn }, _hmac);

        var tenant = "tenant-v";
        var r1 = AuditRecordBuilder.BuildDefault(tenantId: tenant, resourceId: "1");
        var hash1 = _hmac.ComputeHash(r1, null);
        r1 = r1 with { IntegrityHash = hash1, PreviousHash = null };

        var r2 = AuditRecordBuilder.BuildDefault(tenantId: tenant, resourceId: "2");
        var hash2 = _hmac.ComputeHash(r2, hash1);
        r2 = r2 with { IntegrityHash = hash2, PreviousHash = hash1 };

        fakeConn.ReaderQueues.Enqueue(_ => FakeDbDataReaderFactory.Create(r1, r2));

        var from = DateTimeOffset.UtcNow.AddHours(-1);
        var until = DateTimeOffset.UtcNow;
        var result = await verifier.VerifyChainAsync(tenant, from, until);

        result.IsValid.Should().BeTrue();
        result.VerifiedCount.Should().Be(2);
        result.FirstFailedRecordId.Should().BeNull();
        result.FailureReason.Should().BeNull();

        fakeConn.ExecutedCommands.Should().HaveCount(2);
        var rlsCmd = fakeConn.ExecutedCommands[0];
        rlsCmd.CommandText.Should().Contain("set_config('audit.tenant_id'");
        rlsCmd.Parameters["TenantId"].Value.Should().Be(tenant);

        var queryCmd = fakeConn.ExecutedCommands[1];
        queryCmd.CommandText.Should().Contain("FROM audit.records");
        queryCmd.Parameters["TenantId"].Value.Should().Be(tenant);
        queryCmd.Parameters["From"].Value.Should().Be(from);
        queryCmd.Parameters["To"].Value.Should().Be(until);
    }

    [Fact]
    public async Task Store_QueryAsync_WithCursor_AddsCursorConditions()
    {
        var fakeConn = new FakeDbConnection();
        var store = new PostgreSqlAuditStore(new PostgreSqlAuditStoreOptions { ConnectionFactory = () => fakeConn });

        var cursorId = Guid.NewGuid();
        var fromDate = DateTimeOffset.UtcNow.AddDays(-1);
        await store.QueryAsync(new AuditQuery
        {
            TenantId = "tenant-cursor",
            From = fromDate,
            ContinuationToken = AuditCursorToken.Create(System.DateTimeOffset.UtcNow, cursorId)
        });

        var queryCmd = fakeConn.ExecutedCommands[1];
        queryCmd.CommandText.Should().Contain("(occurred_at, id) > (");
        queryCmd.CommandText.Should().Contain("(@CursorDate, @CursorId)");
        queryCmd.Parameters["CursorId"].Value.Should().Be(cursorId);
    }

    [Fact]
    public async Task Verifier_VerifyChainAsync_ChainBreak_ReturnsFalse()
    {
        var fakeConn = new FakeDbConnection();
        var verifier = new PostgreSqlAuditIntegrityVerifier(new PostgreSqlAuditStoreOptions { ConnectionFactory = () => fakeConn }, _hmac);

        var tenant = "tenant-break";
        var r1 = AuditRecordBuilder.BuildDefault(tenantId: tenant, resourceId: "1");
        var hash1 = _hmac.ComputeHash(r1, null);
        r1 = r1 with { IntegrityHash = hash1, PreviousHash = null };

        var r2 = AuditRecordBuilder.BuildDefault(tenantId: tenant, resourceId: "2");
        var hash2 = _hmac.ComputeHash(r2, "wrong_prev");
        r2 = r2 with { IntegrityHash = hash2, PreviousHash = "wrong_prev" };

        fakeConn.ReaderQueues.Enqueue(_ => FakeDbDataReaderFactory.Create(r1, r2));

        var result = await verifier.VerifyChainAsync(tenant, DateTimeOffset.UtcNow.AddHours(-1), DateTimeOffset.UtcNow);

        result.IsValid.Should().BeFalse();
        result.VerifiedCount.Should().Be(2);
        result.FirstFailedRecordId.Should().Be(r2.Id);
        result.FailureReason.Should().Be("Chain break: previous_hash does not match predecessor's integrity_hash.");
    }

    [Fact]
    public async Task Verifier_VerifyChainAsync_TamperedHash_ReturnsFalse()
    {
        var fakeConn = new FakeDbConnection();
        var verifier = new PostgreSqlAuditIntegrityVerifier(new PostgreSqlAuditStoreOptions { ConnectionFactory = () => fakeConn }, _hmac);

        var tenant = "tenant-tamper";
        var r1 = AuditRecordBuilder.BuildDefault(tenantId: tenant, resourceId: "1") with
        {
            IntegrityHash = "INVALID_HASH",
            PreviousHash = null
        };

        fakeConn.ReaderQueues.Enqueue(_ => FakeDbDataReaderFactory.Create(r1));

        var result = await verifier.VerifyChainAsync(tenant, DateTimeOffset.UtcNow.AddHours(-1), DateTimeOffset.UtcNow);

        result.IsValid.Should().BeFalse();
        result.VerifiedCount.Should().Be(1);
        result.FirstFailedRecordId.Should().Be(r1.Id);
        result.FailureReason.Should().Be("Integrity hash mismatch: record content has been tampered with.");
    }

    [Fact]
    public async Task Verifier_VerifyChainAsync_Cancellation_Throws()
    {
        var fakeConn = new FakeDbConnection();
        var verifier = new PostgreSqlAuditIntegrityVerifier(new PostgreSqlAuditStoreOptions { ConnectionFactory = () => fakeConn }, _hmac);

        var rCancel = AuditRecordBuilder.BuildDefault(tenantId: "tenant");
        fakeConn.ReaderQueues.Enqueue(_ => FakeDbDataReaderFactory.Create(rCancel));

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Func<Task> act = async () => await verifier.VerifyChainAsync("tenant", DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow, cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task Store_QueryAsync_ExactPageSizeCount_HasMoreIsFalse()
    {
        var fakeConn = new FakeDbConnection();
        var store = new PostgreSqlAuditStore(new PostgreSqlAuditStoreOptions { ConnectionFactory = () => fakeConn });

        var r1 = AuditRecordBuilder.Create()
            .WithTenant("tenant")
            .WithAction("Create")
            .WithResource("Order", "1")
            .WithOccurredAt(DateTimeOffset.UtcNow.AddMinutes(-2))
            .Build();

        var r2 = AuditRecordBuilder.Create()
            .WithTenant("tenant")
            .WithAction("Create")
            .WithResource("Order", "2")
            .WithOccurredAt(DateTimeOffset.UtcNow.AddMinutes(-1))
            .Build();

        fakeConn.ReaderQueues.Enqueue(_ => FakeDbDataReaderFactory.Create(r1, r2));

        var result = await store.QueryAsync(new AuditQuery { TenantId = "tenant", PageSize = 2 });

        result.Records.Should().HaveCount(2);
        result.HasMore.Should().BeFalse();
        result.NextPageToken.Should().BeNull();
    }

    [Fact]
    public async Task Store_QueryAsync_PageSizeBoundaries_ValidatesRange()
    {
        var fakeConn = new FakeDbConnection();
        var store = new PostgreSqlAuditStore(new PostgreSqlAuditStoreOptions { ConnectionFactory = () => fakeConn });

        // PageSize = 1 and 1000 should not throw ArgumentOutOfRangeException
        var res1 = await store.QueryAsync(new AuditQuery { TenantId = "tenant", PageSize = 1 });
        res1.Records.Should().BeEmpty();

        var res1000 = await store.QueryAsync(new AuditQuery { TenantId = "tenant", PageSize = 1000 });
        res1000.Records.Should().BeEmpty();

        // 0 and 1001 must throw
        Func<Task> act0 = async () => await store.QueryAsync(new AuditQuery { TenantId = "tenant", PageSize = 0 });
        var ex0 = await act0.Should().ThrowAsync<ArgumentOutOfRangeException>();
        ex0.Which.Message.Should().Contain("PageSize must be between 1 and 1000.");

        Func<Task> act1001 = async () => await store.QueryAsync(new AuditQuery { TenantId = "tenant", PageSize = 1001 });
        var ex1001 = await act1001.Should().ThrowAsync<ArgumentOutOfRangeException>();
        ex1001.Which.Message.Should().Contain("PageSize must be between 1 and 1000.");
    }

    [Fact]
    public async Task Store_QueryAsync_WithNullFromAndTo_HandlesDefaults()
    {
        var fakeConn = new FakeDbConnection();
        var store = new PostgreSqlAuditStore(new PostgreSqlAuditStoreOptions { ConnectionFactory = () => fakeConn });

        await store.QueryAsync(new AuditQuery { TenantId = "tenant-nulls", From = null, To = null });

        var queryCmd = fakeConn.ExecutedCommands[1];
        queryCmd.Parameters["MinDate"].Value.Should().Be(DateTimeOffset.UnixEpoch.UtcDateTime);
        queryCmd.CommandText.Should().NotContain("occurred_at <= @MaxDate");
        queryCmd.Parameters.Contains("MaxDate").Should().BeFalse();
    }

    [Fact]
    public async Task Verifier_VerifyChainAsync_SubchainWithPreviousHash_Succeeds()
    {
        var fakeConn = new FakeDbConnection();
        var verifier = new PostgreSqlAuditIntegrityVerifier(new PostgreSqlAuditStoreOptions { ConnectionFactory = () => fakeConn }, _hmac);

        var tenant = "tenant-subchain";
        var r = AuditRecordBuilder.BuildDefault(tenantId: tenant);
        var hash = _hmac.ComputeHash(r, "prior-hash-from-older-record");
        var rSub = r with { IntegrityHash = hash, PreviousHash = "prior-hash-from-older-record" };

        fakeConn.ReaderQueues.Enqueue(_ => FakeDbDataReaderFactory.Create(rSub));

        var result = await verifier.VerifyChainAsync(tenant, DateTimeOffset.UtcNow.AddHours(-1), DateTimeOffset.UtcNow);

        result.IsValid.Should().BeTrue();
        result.VerifiedCount.Should().Be(1);
    }

    [Fact]
    public async Task Store_And_Verifier_WithClosedAndOpenConnections_BehaveCorrectly()
    {
        var closedConn = new FakeDbConnection();
        closedConn.Close();
        var store = new PostgreSqlAuditStore(new PostgreSqlAuditStoreOptions { ConnectionFactory = () => closedConn });
        var verifier = new PostgreSqlAuditIntegrityVerifier(new PostgreSqlAuditStoreOptions { ConnectionFactory = () => closedConn }, _hmac);

        var r = AuditRecordBuilder.BuildDefault(tenantId: "tenant-conn");
        var hash = _hmac.ComputeHash(r, null);
        r = r with { IntegrityHash = hash };
        await store.AppendAsync(r);

        closedConn.State.Should().Be(ConnectionState.Open);

        closedConn.Close();
        closedConn.ReaderQueues.Enqueue(_ => FakeDbDataReaderFactory.Create(r));
        var verifyRes = await verifier.VerifyChainAsync("tenant-conn", DateTimeOffset.UtcNow.AddHours(-1), DateTimeOffset.UtcNow);
        verifyRes.IsValid.Should().BeTrue();
        closedConn.State.Should().Be(ConnectionState.Open);

        var openConn = new FakeDbConnection();
        openConn.Open();
        var storeOpen = new PostgreSqlAuditStore(new PostgreSqlAuditStoreOptions { ConnectionFactory = () => openConn });
        await storeOpen.AppendAsync(r);
    }

    private sealed class NonDbConnectionWrapper : IDbConnection
    {
        private readonly FakeDbConnection _inner;
        public NonDbConnectionWrapper(FakeDbConnection inner) => _inner = inner;
        [System.Diagnostics.CodeAnalysis.AllowNull]
        public string ConnectionString { get => _inner.ConnectionString ?? string.Empty; set => _inner.ConnectionString = value ?? string.Empty; }
        public int ConnectionTimeout => _inner.ConnectionTimeout;
        public string Database => _inner.Database;
        public ConnectionState State => _inner.State;
        public IDbTransaction BeginTransaction() => _inner.BeginTransaction();
        public IDbTransaction BeginTransaction(IsolationLevel il) => _inner.BeginTransaction(il);
        public void ChangeDatabase(string databaseName) => _inner.ChangeDatabase(databaseName);
        public void Close() => _inner.Close();
        public IDbCommand CreateCommand() => _inner.CreateCommand();
        public void Open() => _inner.Open();
        public void Dispose() => _inner.Dispose();
    }

    private sealed class TestSensitivityPipeline : IAuditSensitivityPipeline
    {
        public int SanitizeCount { get; private set; }

        public ValueTask<AuditRecord> SanitizeAsync(AuditRecord record, CancellationToken cancellationToken = default)
        {
            SanitizeCount++;
            return ValueTask.FromResult(record);
        }

        public ValueTask<IReadOnlyList<AuditChange>?> ApplyAsync(IReadOnlyList<AuditChange>? changes, string tenantId, CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(changes);
        }
    }

    [Fact]
    public void PostgreSqlAuditStoreOptions_SchemaAndTable_Validation()
    {
        var options = new PostgreSqlAuditStoreOptions();
        options.Schema.Should().Be("audit");
        options.Table.Should().Be("records");

        options.Schema = "custom_schema";
        options.Schema.Should().Be("custom_schema");
        options.Table = "custom_table";
        options.Table.Should().Be("custom_table");

        Action actEmptySchema = () => options.Schema = "";
        actEmptySchema.Should().Throw<ArgumentException>();

        Action actWhitespaceSchema = () => options.Schema = "   ";
        actWhitespaceSchema.Should().Throw<ArgumentException>();

        Action actInvalidSchema = () => options.Schema = "invalid-schema!";
        actInvalidSchema.Should().Throw<ArgumentException>()
            .WithMessage("*is not a valid SQL identifier*")
            .WithParameterName("value");

        Action actEmptyTable = () => options.Table = "";
        actEmptyTable.Should().Throw<ArgumentException>();

        Action actWhitespaceTable = () => options.Table = "   ";
        actWhitespaceTable.Should().Throw<ArgumentException>();

        Action actInvalidTable = () => options.Table = "invalid-table!";
        actInvalidTable.Should().Throw<ArgumentException>()
            .WithMessage("*is not a valid SQL identifier*")
            .WithParameterName("value");
    }

    [Fact]
    public async Task AppendBatchAsync_EmptyBatch_ReturnsWithoutExecuting()
    {
        var conn = new FakeDbConnection();
        var store = new PostgreSqlAuditStore(new PostgreSqlAuditStoreOptions { ConnectionFactory = () => conn });
        await store.AppendBatchAsync(Array.Empty<AuditRecord>());
        conn.ExecutedCommands.Should().BeEmpty();
    }

    [Fact]
    public async Task Append_And_AppendBatch_WithSensitivityPipeline_SanitizesRecords()
    {
        var conn = new FakeDbConnection();
        var pipeline = new TestSensitivityPipeline();
        var store = new PostgreSqlAuditStore(new PostgreSqlAuditStoreOptions { ConnectionFactory = () => conn }, pipeline);

        var r = AuditRecordBuilder.BuildDefault(tenantId: "tenant-sens");
        await store.AppendAsync(r);
        pipeline.SanitizeCount.Should().Be(1);

        var records = new[]
        {
            AuditRecordBuilder.BuildDefault(tenantId: "tenant-sens"),
            AuditRecordBuilder.BuildDefault(tenantId: "tenant-sens")
        };
        await store.AppendBatchAsync(records);
        pipeline.SanitizeCount.Should().Be(3);
    }

    [Fact]
    public async Task AppendBatchAsync_WithNonDbConnection_OpensAndBeginsTransaction()
    {
        var innerConn = new FakeDbConnection();
        innerConn.Close();
        var wrapper = new NonDbConnectionWrapper(innerConn);
        var store = new PostgreSqlAuditStore(new PostgreSqlAuditStoreOptions { ConnectionFactory = () => wrapper });

        var records = new[] { AuditRecordBuilder.BuildDefault(tenantId: "tenant-nondb") };
        await store.AppendBatchAsync(records);
        innerConn.ExecutedCommands.Should().Contain(c => c.CommandText.Contains("INSERT INTO"));
    }

    [Fact]
    public async Task AppendBatchAsync_WithNonDbConnection_AlreadyOpen_DoesNotCallOpen()
    {
        var innerConn = new FakeDbConnection();
        innerConn.Open();
        var wrapper = new NonDbConnectionWrapper(innerConn);
        var store = new PostgreSqlAuditStore(new PostgreSqlAuditStoreOptions { ConnectionFactory = () => wrapper });

        var records = new[] { AuditRecordBuilder.BuildDefault(tenantId: "tenant-nondb") };
        await store.AppendBatchAsync(records);
        innerConn.ExecutedCommands.Should().Contain(c => c.CommandText.Contains("INSERT INTO"));
    }

    [Fact]
    public async Task Verifier_WithNonDbConnection_OpensConnection()
    {
        var innerConn = new FakeDbConnection();
        innerConn.Close();
        var wrapper = new NonDbConnectionWrapper(innerConn);
        var verifier = new PostgreSqlAuditIntegrityVerifier(new PostgreSqlAuditStoreOptions { ConnectionFactory = () => wrapper }, _hmac);

        var r = AuditRecordBuilder.BuildDefault(tenantId: "tenant-nondb");
        var hash = _hmac.ComputeHash(r, null);
        r = r with { IntegrityHash = hash };

        innerConn.ReaderQueues.Enqueue(_ => FakeDbDataReaderFactory.Create(r));
        var result = await verifier.VerifyChainAsync("tenant-nondb", DateTimeOffset.UtcNow.AddHours(-1), DateTimeOffset.UtcNow);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Verifier_RecordsWithChanges_IncludingRedacted_VerifyCorrectly()
    {
        var conn = new FakeDbConnection();
        var r = AuditRecordBuilder.Create()
            .WithTenant("tenant-changes")
            .WithAction(AuditAction.Update)
            .WithResource("Invoice", "inv-1")
            .AddChange("amount", "100", "200", isRedacted: false)
            .AddRedactedChange("ssn")
            .Build();

        var hash = _hmac.ComputeHash(r, null);
        var signed = r with { IntegrityHash = hash, PreviousHash = null };

        conn.ReaderQueues.Enqueue(_ => FakeDbDataReaderFactory.Create(signed));
        var verifier = new PostgreSqlAuditIntegrityVerifier(new PostgreSqlAuditStoreOptions { ConnectionFactory = () => conn }, _hmac);

        var result = await verifier.VerifyChainAsync("tenant-changes", DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow);
        result.IsValid.Should().BeTrue();
        result.VerifiedCount.Should().Be(1);
        conn.CreatedTransactions.Should().ContainSingle(t => t.Committed);
    }

    [Fact]
    public async Task QueryAsync_WithContinuationToken_SetsCursorDateAndCursorIdParameters()
    {
        var conn = new FakeDbConnection();
        var store = new PostgreSqlAuditStore(new PostgreSqlAuditStoreOptions { ConnectionFactory = () => conn });
        var cursorId = Guid.NewGuid();
        var cursorDate = DateTimeOffset.FromUnixTimeMilliseconds(1700000000000);

        conn.ReaderQueues.Enqueue(_ => FakeDbDataReaderFactory.Create(Array.Empty<AuditRecord>()));

        var query = new AuditQuery
        {
            TenantId = "tenant-cursor",
            ContinuationToken = AuditCursorToken.Create(cursorDate, cursorId)
        };

        var result = await store.QueryAsync(query);
        result.Records.Should().BeEmpty();

        conn.ExecutedCommands.Should().Contain(c => c.CommandText.Contains("(occurred_at, id) > (@CursorDate, @CursorId)"));
        var queryCmd = conn.ExecutedCommands.First(c => c.CommandText.Contains("(occurred_at, id) > (@CursorDate, @CursorId)"));
        queryCmd.Parameters["CursorDate"].Value.Should().Be(cursorDate);
        queryCmd.Parameters["CursorId"].Value.Should().Be(cursorId);
        conn.CreatedTransactions.Should().OnlyContain(t => t.Committed);
    }

    [Fact]
    public async Task Verifier_VerifyChainAsync_CancellationDuringIteration_Throws()
    {
        var conn = new FakeDbConnection { EnforceAsyncTransaction = true };
        var r = AuditRecordBuilder.BuildDefault("tenant-cancelling");
        var hash = _hmac.ComputeHash(r, null);
        var signed = r with { IntegrityHash = hash };

        using var cts = new CancellationTokenSource();
        conn.ReaderQueues.Enqueue(_ =>
        {
            cts.Cancel();
            return FakeDbDataReaderFactory.Create(signed);
        });

        var verifier = new PostgreSqlAuditIntegrityVerifier(new PostgreSqlAuditStoreOptions { ConnectionFactory = () => conn }, _hmac);
        Func<Task> act = async () => await verifier.VerifyChainAsync("tenant-cancelling", DateTimeOffset.UtcNow.AddHours(-1), DateTimeOffset.UtcNow, cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task Verifier_RecordsWithNullAndEmptyChangesJson_VerifyCorrectly()
    {
        var conn = new FakeDbConnection { EnforceAsyncTransaction = true };
        var r = AuditRecordBuilder.Create()
            .WithTenant("tenant-raw-changes")
            .WithAction(AuditAction.Create)
            .WithResource("Order", "ord-1")
            .Build();

        var hash = _hmac.ComputeHash(r, null);
        var signed = r with { IntegrityHash = hash, PreviousHash = null };

        // Test with raw string "null"
        conn.ReaderQueues.Enqueue(_ => FakeDbDataReaderFactory.CreateWithRawChanges(signed, "null", isStringId: false, isStringDate: false));
        var verifier = new PostgreSqlAuditIntegrityVerifier(new PostgreSqlAuditStoreOptions { ConnectionFactory = () => conn }, _hmac);

        var result = await verifier.VerifyChainAsync("tenant-raw-changes", DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow);
        result.IsValid.Should().BeTrue();
        result.VerifiedCount.Should().Be(1);

        // Test with raw string "[]"
        conn.ReaderQueues.Enqueue(_ => FakeDbDataReaderFactory.CreateWithRawChanges(signed, "[]", isStringId: false, isStringDate: false));
        var resultEmpty = await verifier.VerifyChainAsync("tenant-raw-changes", DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow);
        resultEmpty.IsValid.Should().BeTrue();
        resultEmpty.VerifiedCount.Should().Be(1);
    }
}





