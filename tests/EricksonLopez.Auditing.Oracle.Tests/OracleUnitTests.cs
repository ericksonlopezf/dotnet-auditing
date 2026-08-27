// Copyright © Erickson Lopez. MIT License.
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using EricksonLopez.Auditing;
using EricksonLopez.Auditing.Oracle;
using EricksonLopez.Auditing.Testing;
using EricksonLopez.Auditing.Tests.Common;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EricksonLopez.Auditing.Oracle.Tests;

public sealed class OracleUnitTests
{
    private readonly HmacAuditIntegrityService _hmac = new(new TestAuditIntegrityProvider());

    [Fact]
    public void Options_DefaultValues()
    {
        var options = new OracleAuditStoreOptions();
        options.Schema.Should().Be("AUDIT_USER");
        options.Table.Should().Be("AUDIT_RECORDS");
    }

    [Fact]
    public void Extensions_NullArguments_Throw()
    {
        var services = new ServiceCollection();
        var builder = services.AddAuditing();

        Assert.Throws<ArgumentNullException>(() => OracleAuditExtensions.UseOracle(null!, opt => { }));
        Assert.Throws<ArgumentNullException>(() => builder.UseOracle(null!));
    }

    [Fact]
    public void Extensions_UnconfiguredConnectionFactory_ThrowsOnInvocation()
    {
        var services = new ServiceCollection();
        var builder = services.AddAuditing();
        builder.UseOracle(opt => { /* leave ConnectionFactory unconfigured */ });

        var sp = services.BuildServiceProvider();
        var options = sp.GetRequiredService<OracleAuditStoreOptions>();
        Action act = () => options.ConnectionFactory();
        var ex = Assert.Throws<InvalidOperationException>(act);
        ex.Message.Should().Be("OracleAuditStoreOptions.ConnectionFactory must be configured. Call UseOracle(options => options.ConnectionFactory = () => new OracleConnection(...)).");
    }

    [Fact]
    public void Extensions_RegistersServicesCorrectly()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IAuditIntegrityProvider, TestAuditIntegrityProvider>();
        services.AddSingleton<HmacAuditIntegrityService>();
        var builder = services.AddAuditing();

        builder.UseOracle(options =>
        {
            options.ConnectionFactory = () => new FakeDbConnection();
            options.Schema = "CUSTOM_SCHEMA";
            options.Table = "CUSTOM_TABLE";
        });

        var sp = services.BuildServiceProvider();
        var options = sp.GetRequiredService<OracleAuditStoreOptions>();
        options.Schema.Should().Be("CUSTOM_SCHEMA");
        options.Table.Should().Be("CUSTOM_TABLE");

        var store = sp.GetService<IAuditStore>();
        store.Should().NotBeNull();
        store.Should().BeOfType<OracleAuditStore>();

        var verifier = sp.GetService<OracleAuditIntegrityVerifier>();
        verifier.Should().NotBeNull();
    }

    [Fact]
    public void Store_Constructor_NullOptions_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new OracleAuditStore(null!));
    }

    [Fact]
    public async Task Store_AppendAsync_NullRecord_Throws()
    {
        var fakeConn = new FakeDbConnection();
        var store = new OracleAuditStore(new OracleAuditStoreOptions { ConnectionFactory = () => fakeConn });

        Func<Task> act = async () => await store.AppendAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task Store_AppendAsync_AllFieldsPopulated_AllParametersMatched()
    {
        var fakeConn = new FakeDbConnection();
        var store = new OracleAuditStore(new OracleAuditStoreOptions
        {
            ConnectionFactory = () => fakeConn,
            Schema = "MY_SCHEMA",
            Table = "MY_RECORDS"
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
                TenantId: "tenant-oracle-1",
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

        var vpdCmd = fakeConn.ExecutedCommands[0];
        vpdCmd.CommandText.Should().Contain("DBMS_SESSION.SET_IDENTIFIER");
        vpdCmd.Parameters["TenantId"].Value.Should().Be("tenant-oracle-1");

        var insertCmd = fakeConn.ExecutedCommands[1];
        insertCmd.CommandText.Should().Contain("INSERT INTO MY_SCHEMA.MY_RECORDS");
        insertCmd.Parameters["Id"].Value.Should().Be(id.ToString("D"));
        insertCmd.Parameters["OccurredAt"].Value.Should().Be(now);
        insertCmd.Parameters["TenantId"].Value.Should().Be("tenant-oracle-1");
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
        var store = new OracleAuditStore(new OracleAuditStoreOptions { ConnectionFactory = () => fakeConn });

        Func<Task> act = async () => await store.AppendBatchAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task Store_AppendBatchAsync_EmptyRecords_DoesNothing()
    {
        var fakeConn = new FakeDbConnection();
        var store = new OracleAuditStore(new OracleAuditStoreOptions { ConnectionFactory = () => fakeConn });

        await store.AppendBatchAsync(Array.Empty<AuditRecord>());
        fakeConn.ExecutedCommands.Should().BeEmpty();
    }

    [Fact]
    public async Task Store_AppendBatchAsync_DifferentTenants_Throws()
    {
        var fakeConn = new FakeDbConnection();
        var store = new OracleAuditStore(new OracleAuditStoreOptions { ConnectionFactory = () => fakeConn });

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
    public async Task Store_AppendBatchAsync_ValidRecords_ExecutesInsert()
    {
        var fakeConn = new FakeDbConnection();
        var store = new OracleAuditStore(new OracleAuditStoreOptions { ConnectionFactory = () => fakeConn });

        var records = new[]
        {
            AuditRecordBuilder.BuildDefault(tenantId: "tenant-batch", resourceId: "res-1"),
            AuditRecordBuilder.BuildDefault(tenantId: "tenant-batch", resourceId: "res-2")
        };

        await store.AppendBatchAsync(records);

        fakeConn.ExecutedCommands.Should().HaveCount(3);
        fakeConn.ExecutedCommands[0].CommandText.Should().Contain("DBMS_SESSION.SET_IDENTIFIER");
        fakeConn.ExecutedCommands[1].CommandText.Should().Contain("INSERT INTO AUDIT_USER.AUDIT_RECORDS");
        fakeConn.ExecutedCommands[2].CommandText.Should().Contain("INSERT INTO AUDIT_USER.AUDIT_RECORDS");
    }

    [Fact]
    public async Task Store_QueryAsync_ValidationAndEdgeCases()
    {
        var fakeConn = new FakeDbConnection();
        var store = new OracleAuditStore(new OracleAuditStoreOptions { ConnectionFactory = () => fakeConn });

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
        var store = new OracleAuditStore(new OracleAuditStoreOptions
        {
            ConnectionFactory = () => fakeConn,
            Schema = "SEC_AUDIT",
            Table = "EVENTS"
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
            AfterRecordId = cursorId,
            PageSize = 50
        };

        var result = await store.QueryAsync(query);

        fakeConn.ExecutedCommands.Should().HaveCount(2);

        var vpdCmd = fakeConn.ExecutedCommands[0];
        vpdCmd.CommandText.Should().Contain("DBMS_SESSION.SET_IDENTIFIER");
        vpdCmd.Parameters["TenantId"].Value.Should().Be("tenant-filter");

        var queryCmd = fakeConn.ExecutedCommands[1];
        queryCmd.CommandText.Should().Contain("FROM SEC_AUDIT.EVENTS");
        queryCmd.CommandText.Should().Contain("WHERE \"TENANT_ID\" = :TenantId AND \"OCCURRED_AT\" >= :StartDate");
        queryCmd.CommandText.Should().Contain("\"TENANT_ID\" = :TenantId");
        queryCmd.CommandText.Should().Contain("\"OCCURRED_AT\" >= :StartDate");
        queryCmd.CommandText.Should().Contain("\"OCCURRED_AT\" <= :EndDate");
        queryCmd.CommandText.Should().Contain("\"ACTOR_ID\" = :ActorId");
        queryCmd.CommandText.Should().Contain("\"ACTION_CODE\" = :ActionCode");
        queryCmd.CommandText.Should().Contain("\"RESOURCE_TYPE\" = :ResourceType");
        queryCmd.CommandText.Should().Contain("\"RESOURCE_ID\" = :ResourceId");
        queryCmd.CommandText.Should().Contain("\"OUTCOME\" = :Outcome");
        queryCmd.CommandText.Should().Contain("\"CORRELATION_ID\" = :CorrelationId");
        queryCmd.CommandText.Should().Contain("FETCH FIRST 51 ROWS ONLY");

        queryCmd.Parameters["TenantId"].Value.Should().Be("tenant-filter");
        queryCmd.Parameters["StartDate"].Value.Should().Be(now.AddHours(-2));
        queryCmd.Parameters["EndDate"].Value.Should().Be(now);
        queryCmd.Parameters["ActorId"].Value.Should().Be("actor-123");
        queryCmd.Parameters["ActionCode"].Value.Should().Be("Update");
        queryCmd.Parameters["ResourceType"].Value.Should().Be("Document");
        queryCmd.Parameters["ResourceId"].Value.Should().Be("doc-99");
        queryCmd.Parameters["Outcome"].Value.Should().Be((byte)AuditOutcome.Failure);
        queryCmd.Parameters["CorrelationId"].Value.Should().Be("corr-555");
        queryCmd.Parameters["CursorId"].Value.Should().Be(cursorId.ToString());

        result.Records.Should().BeEmpty();
        result.HasMore.Should().BeFalse();
        result.NextCursorId.Should().BeNull();
    }

    [Fact]
    public async Task Store_QueryAsync_EmptyChangesArray_DeserializesToNull()
    {
        var fakeConn = new FakeDbConnection();
        var store = new OracleAuditStore(new OracleAuditStoreOptions { ConnectionFactory = () => fakeConn });

        var tenant = "tenant-empty-changes";
        var id = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        fakeConn.ReaderQueues.Enqueue(_ => FakeDbDataReaderFactory.CreateRaw(
            FakeDbDataReaderFactory.OracleColumns,
            new List<object?[]>
            {
                new object?[] { id.ToString("D"), now.UtcDateTime, tenant, "Src", (byte)1, "act1", "Actor", "Act", "Res", "1", null, null, (byte)1, null, null, null, null, null, null, "[]", "hash", null }
            }));

        var result = await store.QueryAsync(new AuditQuery { TenantId = tenant });

        result.Records.Should().HaveCount(1);
        result.Records[0].Changes.Should().BeNull();
    }

    [Fact]
    public async Task Store_QueryAsync_RowMappingAndPagination()
    {
        var fakeConn = new FakeDbConnection();
        var store = new OracleAuditStore(new OracleAuditStoreOptions { ConnectionFactory = () => fakeConn });

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

        fakeConn.ReaderQueues.Enqueue(_ => FakeDbDataReaderFactory.CreateOracle(r1, r2, r3));

        var queryResult = await store.QueryAsync(new AuditQuery
        {
            TenantId = "tenant-rows",
            PageSize = 2
        });

        queryResult.Records.Should().HaveCount(2);
        queryResult.HasMore.Should().BeTrue();
        queryResult.NextCursorId.Should().Be(r2Id);

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
        var options = new OracleAuditStoreOptions { ConnectionFactory = () => new FakeDbConnection() };
        Assert.Throws<ArgumentNullException>(() => new OracleAuditIntegrityVerifier(null!, _hmac));
        Assert.Throws<ArgumentNullException>(() => new OracleAuditIntegrityVerifier(options, null!));
    }

    [Fact]
    public async Task Verifier_VerifyChainAsync_NullOrEmptyTenant_Throws()
    {
        var fakeConn = new FakeDbConnection();
        var verifier = new OracleAuditIntegrityVerifier(new OracleAuditStoreOptions { ConnectionFactory = () => fakeConn }, _hmac);

        Func<Task> nullTenant = async () => await verifier.VerifyChainAsync(null!, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        await nullTenant.Should().ThrowAsync<ArgumentException>();

        Func<Task> emptyTenant = async () => await verifier.VerifyChainAsync("", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        await emptyTenant.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task Verifier_VerifyChainAsync_ValidChain_Succeeds()
    {
        var fakeConn = new FakeDbConnection();
        var verifier = new OracleAuditIntegrityVerifier(new OracleAuditStoreOptions { ConnectionFactory = () => fakeConn }, _hmac);

        var tenant = "tenant-v";
        var r1 = AuditRecordBuilder.BuildDefault(tenantId: tenant, resourceId: "1");
        var hash1 = _hmac.ComputeHash(r1, null);
        r1 = r1 with { IntegrityHash = hash1, PreviousHash = null };

        var r2 = AuditRecordBuilder.BuildDefault(tenantId: tenant, resourceId: "2");
        var hash2 = _hmac.ComputeHash(r2, hash1);
        r2 = r2 with { IntegrityHash = hash2, PreviousHash = hash1 };

        fakeConn.ReaderQueues.Enqueue(_ => FakeDbDataReaderFactory.CreateOracle(r1, r2));

        var from = DateTimeOffset.UtcNow.AddHours(-1);
        var until = DateTimeOffset.UtcNow;
        var result = await verifier.VerifyChainAsync(tenant, from, until);

        result.IsValid.Should().BeTrue();
        result.VerifiedCount.Should().Be(2);
        result.FirstFailedRecordId.Should().BeNull();
        result.FailureReason.Should().BeNull();

        fakeConn.ExecutedCommands.Should().HaveCount(2);
        var rlsCmd = fakeConn.ExecutedCommands[0];
        rlsCmd.CommandText.Should().Contain("DBMS_SESSION.SET_IDENTIFIER");
        rlsCmd.Parameters["TenantId"].Value.Should().Be(tenant);

        var queryCmd = fakeConn.ExecutedCommands[1];
        queryCmd.CommandText.Should().Contain("FROM AUDIT_USER.AUDIT_RECORDS");
        queryCmd.Parameters["TenantId"].Value.Should().Be(tenant);
        queryCmd.Parameters["StartDate"].Value.Should().Be(from);
        queryCmd.Parameters["EndDate"].Value.Should().Be(until);
    }

    [Fact]
    public async Task Store_QueryAsync_WithCursor_AddsCursorConditions()
    {
        var fakeConn = new FakeDbConnection();
        var store = new OracleAuditStore(new OracleAuditStoreOptions { ConnectionFactory = () => fakeConn });

        var cursorId = Guid.NewGuid();
        var fromDate = DateTimeOffset.UtcNow.AddDays(-1);
        await store.QueryAsync(new AuditQuery
        {
            TenantId = "tenant-cursor",
            From = fromDate,
            AfterRecordId = cursorId
        });

        var queryCmd = fakeConn.ExecutedCommands[1];
        queryCmd.CommandText.Should().Contain("(\"OCCURRED_AT\" > (SELECT \"OCCURRED_AT\" FROM AUDIT_USER.AUDIT_RECORDS WHERE \"ID\" = :CursorId)");
        queryCmd.Parameters["CursorId"].Value.Should().Be(cursorId.ToString("D"));
    }

    [Fact]
    public async Task Verifier_VerifyChainAsync_ChainBreak_ReturnsFalse()
    {
        var fakeConn = new FakeDbConnection();
        var verifier = new OracleAuditIntegrityVerifier(new OracleAuditStoreOptions { ConnectionFactory = () => fakeConn }, _hmac);

        var tenant = "tenant-break";
        var r1 = AuditRecordBuilder.BuildDefault(tenantId: tenant, resourceId: "1");
        var hash1 = _hmac.ComputeHash(r1, null);
        r1 = r1 with { IntegrityHash = hash1, PreviousHash = null };

        var r2 = AuditRecordBuilder.BuildDefault(tenantId: tenant, resourceId: "2");
        var hash2 = _hmac.ComputeHash(r2, "wrong_prev");
        r2 = r2 with { IntegrityHash = hash2, PreviousHash = "wrong_prev" };

        fakeConn.ReaderQueues.Enqueue(_ => FakeDbDataReaderFactory.CreateOracle(r1, r2));

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
        var verifier = new OracleAuditIntegrityVerifier(new OracleAuditStoreOptions { ConnectionFactory = () => fakeConn }, _hmac);

        var tenant = "tenant-tamper";
        var r1 = AuditRecordBuilder.BuildDefault(tenantId: tenant, resourceId: "1") with
        {
            IntegrityHash = "INVALID_HASH",
            PreviousHash = null
        };

        fakeConn.ReaderQueues.Enqueue(_ => FakeDbDataReaderFactory.CreateOracle(r1));

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
        var verifier = new OracleAuditIntegrityVerifier(new OracleAuditStoreOptions { ConnectionFactory = () => fakeConn }, _hmac);

        var rCancel = AuditRecordBuilder.BuildDefault(tenantId: "tenant");
        fakeConn.ReaderQueues.Enqueue(_ => FakeDbDataReaderFactory.CreateOracle(rCancel));

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Func<Task> act = async () => await verifier.VerifyChainAsync("tenant", DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow, cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task Store_QueryAsync_ExactPageSizeCount_HasMoreIsFalse()
    {
        var fakeConn = new FakeDbConnection();
        var store = new OracleAuditStore(new OracleAuditStoreOptions { ConnectionFactory = () => fakeConn });

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

        fakeConn.ReaderQueues.Enqueue(_ => FakeDbDataReaderFactory.CreateOracle(r1, r2));

        var result = await store.QueryAsync(new AuditQuery { TenantId = "tenant", PageSize = 2 });

        result.Records.Should().HaveCount(2);
        result.HasMore.Should().BeFalse();
        result.NextCursorId.Should().BeNull();
    }

    [Fact]
    public async Task Store_QueryAsync_PageSizeBoundaries_ValidatesRange()
    {
        var fakeConn = new FakeDbConnection();
        var store = new OracleAuditStore(new OracleAuditStoreOptions { ConnectionFactory = () => fakeConn });

        var res1 = await store.QueryAsync(new AuditQuery { TenantId = "tenant", PageSize = 1 });
        res1.Records.Should().BeEmpty();

        var res1000 = await store.QueryAsync(new AuditQuery { TenantId = "tenant", PageSize = 1000 });
        res1000.Records.Should().BeEmpty();

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
        var store = new OracleAuditStore(new OracleAuditStoreOptions { ConnectionFactory = () => fakeConn });

        await store.QueryAsync(new AuditQuery { TenantId = "tenant-nulls", From = null, To = null });

        var queryCmd = fakeConn.ExecutedCommands[1];
        queryCmd.Parameters["StartDate"].Value.Should().Be(DateTimeOffset.UnixEpoch);
        queryCmd.CommandText.Should().NotContain("OCCURRED_AT <= :EndDate");
        queryCmd.Parameters.Contains("EndDate").Should().BeFalse();
    }

    [Fact]
    public async Task Verifier_VerifyChainAsync_SubchainWithPreviousHash_Succeeds()
    {
        var fakeConn = new FakeDbConnection();
        var verifier = new OracleAuditIntegrityVerifier(new OracleAuditStoreOptions { ConnectionFactory = () => fakeConn }, _hmac);

        var tenant = "tenant-subchain";
        var r = AuditRecordBuilder.BuildDefault(tenantId: tenant);
        var hash = _hmac.ComputeHash(r, "prior-hash-from-older-record");
        var rSub = r with { IntegrityHash = hash, PreviousHash = "prior-hash-from-older-record" };

        fakeConn.ReaderQueues.Enqueue(_ => FakeDbDataReaderFactory.CreateOracle(rSub));

        var result = await verifier.VerifyChainAsync(tenant, DateTimeOffset.UtcNow.AddHours(-1), DateTimeOffset.UtcNow);

        result.IsValid.Should().BeTrue();
        result.VerifiedCount.Should().Be(1);
    }

    [Fact]
    public async Task Store_And_Verifier_WithClosedAndOpenConnections_BehaveCorrectly()
    {
        var closedConn = new FakeDbConnection();
        closedConn.Close();
        var store = new OracleAuditStore(new OracleAuditStoreOptions { ConnectionFactory = () => closedConn });
        var verifier = new OracleAuditIntegrityVerifier(new OracleAuditStoreOptions { ConnectionFactory = () => closedConn }, _hmac);

        var r = AuditRecordBuilder.BuildDefault(tenantId: "tenant-conn");
        await store.AppendAsync(r);

        closedConn.State.Should().Be(ConnectionState.Open);

        closedConn.Close();
        closedConn.ReaderQueues.Enqueue(_ => FakeDbDataReaderFactory.CreateOracle(r));
        var verRes = await verifier.VerifyChainAsync("tenant-conn", DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow);
        closedConn.State.Should().Be(ConnectionState.Open);

        var openConn = new FakeDbConnection();
        openConn.Open();
        var storeOpen = new OracleAuditStore(new OracleAuditStoreOptions { ConnectionFactory = () => openConn });
        var verifierOpen = new OracleAuditIntegrityVerifier(new OracleAuditStoreOptions { ConnectionFactory = () => openConn }, _hmac);
        await storeOpen.AppendAsync(r);

        openConn.ReaderQueues.Enqueue(_ => FakeDbDataReaderFactory.CreateOracle(r));
        var verOpenRes = await verifierOpen.VerifyChainAsync("tenant-conn", DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task Store_WithEmptySchema_UsesTableDirectly()
    {
        var fakeConn = new FakeDbConnection();
        var store = new OracleAuditStore(new OracleAuditStoreOptions
        {
            ConnectionFactory = () => fakeConn,
            Schema = string.Empty,
            Table = "AUDIT_RECORDS"
        });

        var r = AuditRecordBuilder.BuildDefault(tenantId: "tenant-schema");
        await store.AppendAsync(r);

        fakeConn.ExecutedCommands.Should().Contain(c => c.CommandText.Contains("INTO AUDIT_RECORDS ("));
        fakeConn.ExecutedCommands.Should().NotContain(c => c.CommandText.Contains(".AUDIT_RECORDS"));
    }

    [Fact]
    public async Task Verifier_WithEmptySchema_UsesTableDirectly()
    {
        var fakeConn = new FakeDbConnection();
        var verifier = new OracleAuditIntegrityVerifier(new OracleAuditStoreOptions
        {
            ConnectionFactory = () => fakeConn,
            Schema = string.Empty,
            Table = "AUDIT_RECORDS"
        }, _hmac);

        fakeConn.ReaderQueues.Enqueue(_ => FakeDbDataReaderFactory.CreateOracle());

        var result = await verifier.VerifyChainAsync("tenant-schema", DateTimeOffset.UtcNow.AddHours(-1), DateTimeOffset.UtcNow);
        result.IsValid.Should().BeTrue();

        fakeConn.ExecutedCommands.Should().Contain(c => c.CommandText.Contains("FROM AUDIT_RECORDS"));
        fakeConn.ExecutedCommands.Should().NotContain(c => c.CommandText.Contains(".AUDIT_RECORDS"));
    }
}
