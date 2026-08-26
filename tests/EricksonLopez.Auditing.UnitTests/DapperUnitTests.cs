// Copyright © Erickson Lopez. MIT License.
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using EricksonLopez.Auditing;
using EricksonLopez.Auditing.Dapper;
using EricksonLopez.Auditing.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EricksonLopez.Auditing.UnitTests;

public sealed class DapperUnitTests
{
    private static DapperAuditStore CreateStore(FakeDbConnection connection, string? table = null)
    {
        connection.EnforceOpenOnCreateCommand = true;
        var options = new DapperAuditStoreOptions
        {
            ConnectionFactory = () => connection
        };
        if (table != null)
        {
            options.Table = table;
        }
        return new DapperAuditStore(options);
    }

    [Fact]
    public void DapperAuditStoreOptions_DefaultValues_AreCorrect()
    {
        var options = new DapperAuditStoreOptions();
        options.Table.Should().Be("audit_records");
        options.ConnectionFactory.Should().BeNull();
    }

    [Fact]
    public void Constructor_NullOptions_ThrowsArgumentNullException()
    {
        Action act = () => _ = new DapperAuditStore(null!);
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("options");
    }

    [Theory]
    [InlineData(null, "audit_records")]
    [InlineData("", "audit_records")]
    [InlineData("   ", "audit_records")]
    [InlineData("custom_audit_table", "custom_audit_table")]
    public async Task Constructor_TableConfiguration_SetsExpectedTableNameInSql(string? inputTable, string expectedTable)
    {
        var conn = new FakeDbConnection();
        var options = new DapperAuditStoreOptions
        {
            ConnectionFactory = () => conn,
            Table = inputTable!
        };
        var store = new DapperAuditStore(options);
        var record = AuditRecordBuilder.BuildDefault();

        await store.AppendAsync(record);

        conn.ExecutedCommands.Should().HaveCount(1);
        conn.ExecutedCommands[0].CommandText.Should().Contain($"INSERT INTO {expectedTable}");
    }

    [Fact]
    public async Task EnsureOpenConnection_OpensClosedConnection_AndDoesNotReopenOpenConnection()
    {
        // AppendAsync
        var closedConn1 = new FakeDbConnection();
        closedConn1.State.Should().Be(ConnectionState.Closed);
        var store1 = CreateStore(closedConn1);
        await store1.AppendAsync(AuditRecordBuilder.BuildDefault());
        closedConn1.OpenCount.Should().Be(1);

        var openConn1 = new FakeDbConnection();
        openConn1.SetState(ConnectionState.Open);
        var store2 = CreateStore(openConn1);
        await store2.AppendAsync(AuditRecordBuilder.BuildDefault());
        openConn1.OpenCount.Should().Be(0);

        // AppendBatchAsync
        var closedConn2 = new FakeDbConnection();
        var store3 = CreateStore(closedConn2);
        await store3.AppendBatchAsync(new[] { AuditRecordBuilder.BuildDefault() });
        closedConn2.OpenCount.Should().Be(1);

        // QueryAsync
        var closedConn3 = new FakeDbConnection();
        closedConn3.ReaderQueues.Enqueue(_ => FakeDbDataReaderFactory.Create(Array.Empty<AuditRecord>()));
        var store4 = CreateStore(closedConn3);
        await store4.QueryAsync(new AuditQuery { TenantId = "t1" });
        closedConn3.OpenCount.Should().Be(1);

        // GetByIdAsync
        var closedConn4 = new FakeDbConnection();
        closedConn4.ReaderQueues.Enqueue(_ => FakeDbDataReaderFactory.Create(Array.Empty<AuditRecord>()));
        var store5 = CreateStore(closedConn4);
        await store5.GetByIdAsync(Guid.NewGuid(), "t1");
        closedConn4.OpenCount.Should().Be(1);
    }

    [Fact]
    public async Task AppendAsync_ExecutesExpectedSqlAndAllParameters()
    {
        var conn = new FakeDbConnection();
        var store = CreateStore(conn);
        var recordId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var record = AuditRecordBuilder.Create()
            .WithId(recordId)
            .WithOccurredAt(now)
            .WithTenant("tenant-a")
            .WithSource("OrderService")
            .WithActor(AuditActorType.User, "user-123", "Alice")
            .WithAction(AuditAction.Create)
            .WithResource("Order", "ord-1", "Customer", "cust-1")
            .WithOutcome(AuditOutcome.Success)
            .WithErrorCode("ERR-01")
            .WithCorrelationId("corr-1")
            .WithCausationId("cause-1")
            .WithRequestId("req-1")
            .WithIpAddress("127.0.0.1")
            .WithUserAgent("TestAgent")
            .WithIntegrityHash("hash123")
            .WithPreviousHash("prev123")
            .AddChange("Status", "Draft", "Submitted")
            .AddRedactedChange("SecretField")
            .Build();

        await store.AppendAsync(record);

        conn.ExecutedCommands.Should().HaveCount(1);
        var cmd = conn.ExecutedCommands[0];
        cmd.CommandText.Should().Contain("INSERT INTO audit_records");
        cmd.Parameters["Id"].Value.Should().Be(recordId);
        cmd.Parameters["OccurredAt"].Value.Should().Be(record.OccurredAt);
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
        cmd.Parameters["ErrorCode"].Value.Should().Be("ERR-01");
        cmd.Parameters["CorrelationId"].Value.Should().Be("corr-1");
        cmd.Parameters["CausationId"].Value.Should().Be("cause-1");
        cmd.Parameters["RequestId"].Value.Should().Be("req-1");
        cmd.Parameters["IpAddress"].Value.Should().Be("127.0.0.1");
        cmd.Parameters["UserAgent"].Value.Should().Be("TestAgent");
        cmd.Parameters["IntegrityHash"].Value.Should().Be("hash123");
        cmd.Parameters["PreviousHash"].Value.Should().Be("prev123");

        var changesJson = cmd.Parameters["Changes"].Value as string;
        changesJson.Should().NotBeNull();
        changesJson.Should().Contain("\"field\":\"Status\"");
        changesJson.Should().Contain("\"oldValue\":\"Draft\"");
        changesJson.Should().Contain("\"newValue\":\"Submitted\"");
        changesJson.Should().Contain("\"isRedacted\":false");
        changesJson.Should().Contain("\"field\":\"SecretField\"");
        changesJson.Should().Contain("\"isRedacted\":true");
    }

    [Fact]
    public async Task AppendAsync_RecordWithNullChangesOrEmptyChanges_SerializesAsNull()
    {
        var conn1 = new FakeDbConnection();
        var store1 = CreateStore(conn1);
        var r1 = AuditRecordBuilder.Create().WithChanges(null).Build();
        await store1.AppendAsync(r1);
        var val1 = conn1.ExecutedCommands[0].Parameters["Changes"].Value;
        (val1 == null || val1 == DBNull.Value).Should().BeTrue();

        var conn2 = new FakeDbConnection();
        var store2 = CreateStore(conn2);
        var r2 = AuditRecordBuilder.Create().WithChanges(Array.Empty<AuditChange>()).Build();
        await store2.AppendAsync(r2);
        var val2 = conn2.ExecutedCommands[0].Parameters["Changes"].Value;
        (val2 == null || val2 == DBNull.Value).Should().BeTrue();
    }

    [Fact]
    public async Task AppendAsync_NullRecord_ThrowsArgumentNullException()
    {
        var conn = new FakeDbConnection();
        var store = CreateStore(conn);

        Func<Task> act = async () => await store.AppendAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("record");
    }

    [Fact]
    public async Task AppendBatchAsync_EmptyList_DoesNotOpenConnectionOrExecute()
    {
        var connectionFactoryCalled = false;
        var store = new DapperAuditStore(new DapperAuditStoreOptions
        {
            ConnectionFactory = () =>
            {
                connectionFactoryCalled = true;
                return new FakeDbConnection();
            }
        });

        await store.AppendBatchAsync(Array.Empty<AuditRecord>());

        connectionFactoryCalled.Should().BeFalse();
    }

    [Fact]
    public async Task AppendBatchAsync_NullRecords_ThrowsArgumentNullException()
    {
        var conn = new FakeDbConnection();
        var store = CreateStore(conn);

        Func<Task> act = async () => await store.AppendBatchAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("records");
    }

    [Fact]
    public async Task AppendBatchAsync_InsertsAllRecordsWithinTransaction_AndCommits()
    {
        var conn = new FakeDbConnection();
        var store = CreateStore(conn);
        var r1 = AuditRecordBuilder.BuildDefault(resourceId: "1");
        var r2 = AuditRecordBuilder.BuildDefault(resourceId: "2");

        await store.AppendBatchAsync(new[] { r1, r2 });

        conn.ExecutedCommands.Should().HaveCount(2);
        conn.ExecutedCommands[0].CommandText.Should().Contain("INSERT INTO audit_records");
        conn.ExecutedCommands[1].CommandText.Should().Contain("INSERT INTO audit_records");
        conn.CreatedTransactions.Should().HaveCount(1);
        conn.CreatedTransactions[0].Committed.Should().BeTrue();
    }

    [Fact]
    public async Task QueryAsync_NullQuery_ThrowsArgumentNullException()
    {
        var conn = new FakeDbConnection();
        var store = CreateStore(conn);

        Func<Task> act = async () => await store.QueryAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("query");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task QueryAsync_NullOrWhitespaceTenantId_ThrowsArgumentException(string? tenantId)
    {
        var conn = new FakeDbConnection();
        var store = CreateStore(conn);

        Func<Task> act = async () => await store.QueryAsync(new AuditQuery { TenantId = tenantId! });
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task QueryAsync_OnlyTenantId_BuildsCorrectWhereClause()
    {
        var conn = new FakeDbConnection();
        var store = CreateStore(conn);

        conn.ReaderQueues.Enqueue(_ => FakeDbDataReaderFactory.Create(Array.Empty<AuditRecord>()));

        var result = await store.QueryAsync(new AuditQuery { TenantId = "tenant-solo" });

        result.Records.Should().BeEmpty();
        conn.ExecutedCommands.Should().HaveCount(1);
        var cmd = conn.ExecutedCommands[0];
        cmd.CommandText.Should().Contain("WHERE tenant_id = @TenantId");
        cmd.CommandText.Should().Contain("ORDER BY id ASC");
        cmd.CommandText.Should().Contain("LIMIT @Limit");
        cmd.Parameters["TenantId"].Value.Should().Be("tenant-solo");
    }

    [Fact]
    public async Task QueryAsync_BuildsAllFilterConditionsCorrectly_WithAndSeparator()
    {
        var conn = new FakeDbConnection();
        var store = CreateStore(conn);
        var cursorId = Guid.NewGuid();
        var fromDate = DateTimeOffset.UtcNow.AddDays(-7);
        var toDate = DateTimeOffset.UtcNow;

        conn.ReaderQueues.Enqueue(_ => FakeDbDataReaderFactory.Create(Array.Empty<AuditRecord>()));

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
        cmd.CommandText.Should().Contain("tenant_id = @TenantId AND actor_id = @ActorId AND action_code = @ActionCode AND resource_type = @ResourceType AND resource_id = @ResourceId AND outcome = @Outcome AND occurred_at >= @From AND occurred_at <= @To AND correlation_id = @CorrelationId AND id > @AfterRecordId");
        cmd.CommandText.Should().Contain("LIMIT @Limit");

        cmd.Parameters["TenantId"].Value.Should().Be("tenant-a");
        cmd.Parameters["ActorId"].Value.Should().Be("actor-1");
        cmd.Parameters["ActionCode"].Value.Should().Be("Create");
        cmd.Parameters["ResourceType"].Value.Should().Be("Invoice");
        cmd.Parameters["ResourceId"].Value.Should().Be("inv-001");
        cmd.Parameters["Outcome"].Value.Should().Be((byte)AuditOutcome.Failure);
        cmd.Parameters["From"].Value.Should().Be(fromDate);
        cmd.Parameters["To"].Value.Should().Be(toDate);
        cmd.Parameters["CorrelationId"].Value.Should().Be("corr-xyz");
        cmd.Parameters["AfterRecordId"].Value.Should().Be(cursorId);
        cmd.Parameters["Limit"].Value.Should().Be(26); // PageSize + 1
    }

    [Theory]
    [InlineData(0, 2)]      // clamped to 1 -> Limit = 2
    [InlineData(-5, 2)]     // clamped to 1 -> Limit = 2
    [InlineData(50, 51)]    // 50 -> Limit = 51
    [InlineData(1500, 1001)]// clamped to 1000 -> Limit = 1001
    public async Task QueryAsync_PageSizeClamping_CalculatesExpectedLimit(int pageSize, int expectedLimit)
    {
        var conn = new FakeDbConnection();
        var store = CreateStore(conn);
        conn.ReaderQueues.Enqueue(_ => FakeDbDataReaderFactory.Create(Array.Empty<AuditRecord>()));

        await store.QueryAsync(new AuditQuery
        {
            TenantId = "tenant-a",
            PageSize = pageSize
        });

        conn.ExecutedCommands.Should().HaveCount(1);
        conn.ExecutedCommands[0].Parameters["Limit"].Value.Should().Be(expectedLimit);
    }

    [Fact]
    public async Task QueryAsync_Pagination_HasMoreAndNextCursor_WhenRowsExceedPageSize()
    {
        var conn = new FakeDbConnection();
        var store = CreateStore(conn);
        var r1 = AuditRecordBuilder.BuildDefault(resourceId: "1");
        var r2 = AuditRecordBuilder.BuildDefault(resourceId: "2");
        var r3 = AuditRecordBuilder.BuildDefault(resourceId: "3");

        // 3 rows returned when PageSize is 2 -> hasMore = true, NextCursorId = r2.Id
        conn.ReaderQueues.Enqueue(_ => FakeDbDataReaderFactory.Create(r1, r2, r3));

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
    public async Task QueryAsync_Pagination_NoMore_WhenRowsEqualOrLessThanPageSize()
    {
        var conn1 = new FakeDbConnection();
        var store1 = CreateStore(conn1);
        var r1 = AuditRecordBuilder.BuildDefault(resourceId: "1");
        var r2 = AuditRecordBuilder.BuildDefault(resourceId: "2");

        // Exactly 2 rows returned when PageSize is 2 -> hasMore = false, NextCursorId = null
        conn1.ReaderQueues.Enqueue(_ => FakeDbDataReaderFactory.Create(r1, r2));
        var result1 = await store1.QueryAsync(new AuditQuery { TenantId = "tenant-a", PageSize = 2 });
        result1.Records.Should().HaveCount(2);
        result1.HasMore.Should().BeFalse();
        result1.NextCursorId.Should().BeNull();

        // 0 rows returned
        var conn2 = new FakeDbConnection();
        var store2 = CreateStore(conn2);
        conn2.ReaderQueues.Enqueue(_ => FakeDbDataReaderFactory.Create(Array.Empty<AuditRecord>()));
        var result2 = await store2.QueryAsync(new AuditQuery { TenantId = "tenant-a", PageSize = 2 });
        result2.Records.Should().BeEmpty();
        result2.HasMore.Should().BeFalse();
        result2.NextCursorId.Should().BeNull();
    }

    [Fact]
    public async Task QueryAsync_MapsRowChanges_CorrectlyForJsonNullAndEmptyArray()
    {
        var conn = new FakeDbConnection();
        var store = CreateStore(conn);
        var r1 = AuditRecordBuilder.Create().WithResource("Res", "1").AddChange("F1", "O1", "N1").Build();
        var r2 = AuditRecordBuilder.Create().WithResource("Res", "2").WithChanges(null).Build();

        conn.ReaderQueues.Enqueue(_ => FakeDbDataReaderFactory.Create(r1, r2));

        var result = await store.QueryAsync(new AuditQuery { TenantId = "tenant-a", PageSize = 10 });

        result.Records.Should().HaveCount(2);
        result.Records[0].Changes.Should().HaveCount(1);
        result.Records[0].Changes![0].Field.Should().Be("F1");
        result.Records[1].Changes.Should().BeNull();
    }

    [Fact]
    public async Task GetByIdAsync_Found_ReturnsMappedRecordAndSql()
    {
        var conn = new FakeDbConnection();
        var store = CreateStore(conn);
        var record = AuditRecordBuilder.Create()
            .WithId(Guid.NewGuid())
            .WithTenant("tenant-a")
            .WithSource("PaymentGateway")
            .WithActor(AuditActorType.Service, "svc-pay", "Payment API")
            .WithAction(AuditAction.Approve)
            .WithResource("Payment", "pay-1", "Order", "ord-9")
            .WithOutcome(AuditOutcome.Success)
            .WithErrorCode("ERR-None")
            .WithCorrelationId("corr-1")
            .WithCausationId("cause-1")
            .WithRequestId("req-1")
            .WithIpAddress("10.0.0.1")
            .WithUserAgent("AgentX")
            .WithIntegrityHash("h1")
            .WithPreviousHash("p1")
            .AddChange("Amount", "100", "200")
            .Build();

        conn.ReaderQueues.Enqueue(_ => FakeDbDataReaderFactory.Create(record));

        var result = await store.GetByIdAsync(record.Id, "tenant-a");

        conn.ExecutedCommands.Should().HaveCount(1);
        var cmd = conn.ExecutedCommands[0];
        cmd.CommandText.Should().Contain("WHERE id = @Id AND tenant_id = @TenantId");
        cmd.CommandText.Should().Contain("LIMIT 1");

        result.Should().NotBeNull();
        result!.Id.Should().Be(record.Id);
        result.Context.TenantId.Should().Be("tenant-a");
        result.Context.Source.Should().Be("PaymentGateway");
        result.Context.CorrelationId.Should().Be("corr-1");
        result.Context.CausationId.Should().Be("cause-1");
        result.Context.RequestId.Should().Be("req-1");
        result.Context.IpAddress.Should().Be("10.0.0.1");
        result.Context.UserAgent.Should().Be("AgentX");
        result.Actor.Type.Should().Be(AuditActorType.Service);
        result.Actor.Id.Should().Be("svc-pay");
        result.Actor.DisplayName.Should().Be("Payment API");
        result.Action.Code.Should().Be("Approve");
        result.Resource.Type.Should().Be("Payment");
        result.Resource.Id.Should().Be("pay-1");
        result.Resource.AggregateType.Should().Be("Order");
        result.Resource.AggregateId.Should().Be("ord-9");
        result.Outcome.Should().Be(AuditOutcome.Success);
        result.ErrorCode.Should().Be("ERR-None");
        result.IntegrityHash.Should().Be("h1");
        result.PreviousHash.Should().Be("p1");
        result.Changes.Should().HaveCount(1);
        result.Changes![0].Field.Should().Be("Amount");
        result.Changes![0].OldValue.Should().Be("100");
        result.Changes![0].NewValue.Should().Be("200");
        result.Changes![0].IsRedacted.Should().BeFalse();
    }

    [Fact]
    public async Task GetByIdAsync_EmptyJsonArrayChanges_ReturnsNullChanges()
    {
        var conn = new FakeDbConnection();
        var store = CreateStore(conn);
        var recordId = Guid.NewGuid();

        // Custom reader returning ChangesJson = "[]"
        var rows = new List<object?[]>
        {
            new object?[]
            {
                recordId,
                DateTimeOffset.UtcNow.UtcDateTime,
                "tenant-a",
                "Source",
                (byte)AuditActorType.User,
                "user-1",
                null,
                "Action",
                "Res",
                "res-1",
                null,
                null,
                (byte)AuditOutcome.Success,
                null,
                null,
                null,
                null,
                null,
                null,
                "[]", // empty json array
                null,
                null
            }
        };
        conn.ReaderQueues.Enqueue(_ => FakeDbDataReaderFactory.CreateRaw(FakeDbDataReaderFactory.StandardColumns, rows));

        var result = await store.GetByIdAsync(recordId, "tenant-a");

        result.Should().NotBeNull();
        result!.Changes.Should().BeNull();
    }

    [Fact]
    public async Task GetByIdAsync_LiteralNullJsonChanges_ReturnsNullChanges()
    {
        var conn = new FakeDbConnection();
        var store = CreateStore(conn);
        var recordId = Guid.NewGuid();

        var rows = new List<object?[]>
        {
            new object?[]
            {
                recordId,
                DateTimeOffset.UtcNow.UtcDateTime,
                "tenant-a",
                "Source",
                (byte)AuditActorType.User,
                "user-1",
                null,
                "Action",
                "Res",
                "res-1",
                null,
                null,
                (byte)AuditOutcome.Success,
                null,
                null,
                null,
                null,
                null,
                null,
                "null", // JSON literal null
                null,
                null
            }
        };
        conn.ReaderQueues.Enqueue(_ => FakeDbDataReaderFactory.CreateRaw(FakeDbDataReaderFactory.StandardColumns, rows));

        var result = await store.GetByIdAsync(recordId, "tenant-a");

        result.Should().NotBeNull();
        result!.Changes.Should().BeNull();
    }

    [Fact]
    public async Task GetByIdAsync_NotFound_ReturnsNull()
    {
        var conn = new FakeDbConnection();
        var store = CreateStore(conn);

        conn.ReaderQueues.Enqueue(_ => FakeDbDataReaderFactory.Create(Array.Empty<AuditRecord>()));

        var result = await store.GetByIdAsync(Guid.NewGuid(), "tenant-a");

        result.Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetByIdAsync_NullOrWhitespaceTenantId_ThrowsArgumentException(string? tenantId)
    {
        var conn = new FakeDbConnection();
        var store = CreateStore(conn);

        Func<Task> act = async () => await store.GetByIdAsync(Guid.NewGuid(), tenantId!);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public void DapperAuditExtensions_UseDapper_RegistersServicesSuccessfully_WithChaining()
    {
        var services = new ServiceCollection();
        var builder = services.AddAuditing();

        var returnedBuilder = builder.UseDapper(options =>
        {
            options.ConnectionFactory = () => new FakeDbConnection();
            options.Table = "custom_audit_records";
        });

        returnedBuilder.Should().BeSameAs(builder);

        var provider = services.BuildServiceProvider();
        provider.GetService<IAuditStore>().Should().BeOfType<DapperAuditStore>();
        provider.GetService<DapperAuditStoreOptions>().Should().NotBeNull();
        provider.GetService<DapperAuditStoreOptions>()!.Table.Should().Be("custom_audit_records");

        // Verify service descriptors have Singleton lifetime
        var storeDescriptor = services.FirstOrDefault(s => s.ServiceType == typeof(IAuditStore));
        storeDescriptor.Should().NotBeNull();
        storeDescriptor!.Lifetime.Should().Be(ServiceLifetime.Singleton);

        var optionsDescriptor = services.FirstOrDefault(s => s.ServiceType == typeof(DapperAuditStoreOptions));
        optionsDescriptor.Should().NotBeNull();
        optionsDescriptor!.Lifetime.Should().Be(ServiceLifetime.Singleton);
    }

    [Fact]
    public void DapperAuditExtensions_NullBuilder_ThrowsArgumentNullException()
    {
        IAuditBuilder builder = null!;
        Action act = () => builder.UseDapper(_ => { });
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("builder");
    }

    [Fact]
    public void DapperAuditExtensions_NullConfigure_ThrowsArgumentNullException()
    {
        var services = new ServiceCollection();
        var builder = services.AddAuditing();

        Action act = () => builder.UseDapper(null!);
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("configure");
    }

    [Fact]
    public void DapperAuditExtensions_MissingConnectionFactory_ThrowsInvalidOperationException_WithExactMessage()
    {
        var services = new ServiceCollection();
        var builder = services.AddAuditing();

        Action act = () => builder.UseDapper(options =>
        {
            // Do not set ConnectionFactory
        });

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("DapperAuditStoreOptions.ConnectionFactory must be configured. Call UseDapper(options => options.ConnectionFactory = () => new DbConnection(...)).");
    }
}
