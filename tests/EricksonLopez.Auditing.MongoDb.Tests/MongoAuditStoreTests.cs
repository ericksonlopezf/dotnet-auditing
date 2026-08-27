// Copyright © Erickson Lopez. MIT License.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using EricksonLopez.Auditing;
using EricksonLopez.Auditing.MongoDb;
using EricksonLopez.Auditing.Testing;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using NSubstitute;
using Xunit;

namespace EricksonLopez.Auditing.MongoDb.Tests;

public sealed class MongoAuditStoreTests
{
    static MongoAuditStoreTests()
    {
        BsonSerializer.TryRegisterSerializer(new MongoDB.Bson.Serialization.Serializers.GuidSerializer(GuidRepresentation.Standard));
    }

    private static BsonDocument RenderFilter(FilterDefinition<MongoAuditRecordDocument> filter)
    {
        var serializerRegistry = BsonSerializer.SerializerRegistry;
        var documentSerializer = serializerRegistry.GetSerializer<MongoAuditRecordDocument>();
        return filter.Render(new RenderArgs<MongoAuditRecordDocument>(documentSerializer, serializerRegistry));
    }

    [Fact]
    public void Options_DefaultValues()
    {
        var options = new MongoAuditStoreOptions();
        options.CollectionName.Should().Be("audit_records");
        options.DatabaseName.Should().Be("AuditingDb");
    }

    [Fact]
    public void Constructor_NullArguments_Throw()
    {
        IMongoCollection<MongoAuditRecordDocument> nullCol = null!;
        var act1 = () => new MongoAuditStore(nullCol);
        act1.Should().Throw<ArgumentNullException>().WithParameterName("collection");

        IMongoDatabase nullDb = null!;
        var act2 = () => new MongoAuditStore(nullDb, new MongoAuditStoreOptions());
        act2.Should().Throw<ArgumentNullException>().WithParameterName("database");

        var act3 = () => new MongoAuditStore(Substitute.For<IMongoDatabase>(), null!);
        act3.Should().Throw<ArgumentNullException>().WithParameterName("options");
    }

    [Fact]
    public void Constructor_WithDatabaseAndOptions_ConstructsStore()
    {
        var database = Substitute.For<IMongoDatabase>();
        var collection = Substitute.For<IMongoCollection<MongoAuditRecordDocument>>();
        database.GetCollection<MongoAuditRecordDocument>("my_col").Returns(collection);

        var store = new MongoAuditStore(database, new MongoAuditStoreOptions { CollectionName = "my_col" });
        store.Should().NotBeNull();
        database.Received(1).GetCollection<MongoAuditRecordDocument>("my_col");
    }

    [Fact]
    public async Task AppendAsync_CallsInsertOneAsync_OnCollection()
    {
        var collection = Substitute.For<IMongoCollection<MongoAuditRecordDocument>>();
        var store = new MongoAuditStore(collection);

        var record = AuditRecordBuilder.Create()
            .WithTenant("tenant-x")
            .WithActor(AuditActorType.User, "usr-9", "Grace")
            .WithAction(AuditAction.Create)
            .WithResource("Document", "doc-1", "Folder", "f-1")
            .WithOutcome(AuditOutcome.Success)
            .WithErrorCode("NONE")
            .WithCorrelationId("c-9")
            .WithCausationId("caus-9")
            .WithRequestId("req-9")
            .WithIpAddress("127.0.0.1")
            .WithUserAgent("Agent/1.0")
            .WithIntegrityHash("hash-9")
            .WithPreviousHash("prev-8")
            .AddChange("Title", null, "New Doc")
            .AddRedactedChange("SecretKey")
            .Build();

        await store.AppendAsync(record);

        await collection.Received(1).InsertOneAsync(
            Arg.Is<MongoAuditRecordDocument>(d =>
                d.Id == record.Id &&
                d.TenantId == "tenant-x" &&
                d.ActorId == "usr-9" &&
                d.ActorName == "Grace" &&
                d.ActorType == (byte)AuditActorType.User &&
                d.ActionCode == "Create" &&
                d.ResourceType == "Document" &&
                d.ResourceId == "doc-1" &&
                d.AggregateType == "Folder" &&
                d.AggregateId == "f-1" &&
                d.Outcome == (byte)AuditOutcome.Success &&
                d.ErrorCode == "NONE" &&
                d.CorrelationId == "c-9" &&
                d.CausationId == "caus-9" &&
                d.RequestId == "req-9" &&
                d.IpAddress == "127.0.0.1" &&
                d.UserAgent == "Agent/1.0" &&
                d.IntegrityHash == "hash-9" &&
                d.PreviousHash == "prev-8" &&
                d.Changes != null &&
                d.Changes.Count == 2 &&
                d.Changes[0].Field == "Title" &&
                d.Changes[0].OldValue == null &&
                d.Changes[0].NewValue == "New Doc" &&
                !d.Changes[0].IsRedacted &&
                d.Changes[1].Field == "SecretKey" &&
                d.Changes[1].IsRedacted),
            cancellationToken: Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AppendAsync_NullRecord_ThrowsArgumentNullException()
    {
        var collection = Substitute.For<IMongoCollection<MongoAuditRecordDocument>>();
        var store = new MongoAuditStore(collection);

        var act = async () => await store.AppendAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>().WithParameterName("record");
    }

    [Fact]
    public async Task AppendAsync_NullOrEmptyChanges_SetsChangesNullInDocument()
    {
        var collection = Substitute.For<IMongoCollection<MongoAuditRecordDocument>>();
        var store = new MongoAuditStore(collection);

        // Record with null changes
        var rec1 = AuditRecordBuilder.BuildDefault(tenantId: "t1");
        await store.AppendAsync(rec1);

        await collection.Received(1).InsertOneAsync(
            Arg.Is<MongoAuditRecordDocument>(d => d.Changes == null),
            cancellationToken: Arg.Any<CancellationToken>());

        // Record with empty changes list
        var rec2 = new AuditRecord
        {
            Id = Guid.NewGuid(),
            OccurredAt = DateTimeOffset.UtcNow,
            Context = new AuditContext("t2", "App"),
            Actor = AuditActor.System,
            Action = AuditAction.Create,
            Resource = new AuditResource("Item", "i1"),
            Outcome = AuditOutcome.Success,
            Changes = new List<AuditChange>()
        };
        await store.AppendAsync(rec2);

        await collection.Received(1).InsertOneAsync(
            Arg.Is<MongoAuditRecordDocument>(d => d.TenantId == "t2" && d.Changes == null),
            cancellationToken: Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AppendBatchAsync_CallsInsertManyAsync_OnCollection()
    {
        var collection = Substitute.For<IMongoCollection<MongoAuditRecordDocument>>();
        var store = new MongoAuditStore(collection);

        var records = new List<AuditRecord>
        {
            AuditRecordBuilder.BuildDefault(tenantId: "tenant-y", resourceId: "cfg-1"),
            AuditRecordBuilder.BuildDefault(tenantId: "tenant-y", resourceId: "cfg-2")
        };

        await store.AppendBatchAsync(records);

        await collection.Received(1).InsertManyAsync(
            Arg.Is<IEnumerable<MongoAuditRecordDocument>>(docs => docs != null && docs.Count() == 2),
            cancellationToken: Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AppendBatchAsync_NullRecords_ThrowsArgumentNullException()
    {
        var collection = Substitute.For<IMongoCollection<MongoAuditRecordDocument>>();
        var store = new MongoAuditStore(collection);

        var act = async () => await store.AppendBatchAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>().WithParameterName("records");
    }

    [Fact]
    public async Task AppendBatchAsync_EmptyList_DoesNotCallInsertMany()
    {
        var collection = Substitute.For<IMongoCollection<MongoAuditRecordDocument>>();
        var store = new MongoAuditStore(collection);

        await store.AppendBatchAsync(Array.Empty<AuditRecord>());

        await collection.DidNotReceive().InsertManyAsync(
            Arg.Any<IEnumerable<MongoAuditRecordDocument>>(),
            cancellationToken: Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task QueryAsync_NullQuery_ThrowsArgumentNullException()
    {
        var collection = Substitute.For<IMongoCollection<MongoAuditRecordDocument>>();
        var store = new MongoAuditStore(collection);

        var act = async () => await store.QueryAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>().WithParameterName("query");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task QueryAsync_NullOrWhitespaceTenantId_ThrowsArgumentException(string? tenantId)
    {
        var collection = Substitute.For<IMongoCollection<MongoAuditRecordDocument>>();
        var store = new MongoAuditStore(collection);

        var act = async () => await store.QueryAsync(new AuditQuery { TenantId = tenantId! });
        await act.Should().ThrowAsync<ArgumentException>().WithParameterName("query.TenantId");
    }

    [Fact]
    public async Task QueryAsync_WithAllFilters_ConstructsCorrectBsonFilterAndLimit()
    {
        var collection = Substitute.For<IMongoCollection<MongoAuditRecordDocument>>();
        var store = new MongoAuditStore(collection);

        FilterDefinition<MongoAuditRecordDocument> capturedFilter = null!;
        FindOptions<MongoAuditRecordDocument, MongoAuditRecordDocument> capturedOptions = null!;

        var cursor = Substitute.For<IAsyncCursor<MongoAuditRecordDocument>>();
        cursor.MoveNextAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(true), Task.FromResult(false));
        cursor.Current.Returns(new List<MongoAuditRecordDocument>());

        collection.FindAsync(
            Arg.Do<FilterDefinition<MongoAuditRecordDocument>>(f => capturedFilter = f),
            Arg.Do<FindOptions<MongoAuditRecordDocument, MongoAuditRecordDocument>>(o => capturedOptions = o),
            Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(cursor));

        var fromDate = DateTimeOffset.UtcNow.AddDays(-2);
        var toDate = DateTimeOffset.UtcNow;
        var afterId = Guid.NewGuid();

        var query = new AuditQuery
        {
            TenantId = "tenant-all",
            ActorId = "user-1",
            ActionCode = "Create",
            ResourceType = "Order",
            ResourceId = "ord-1",
            Outcome = AuditOutcome.Success,
            From = fromDate,
            To = toDate,
            CorrelationId = "corr-1",
            AfterRecordId = afterId,
            PageSize = 25
        };

        var result = await store.QueryAsync(query);

        capturedFilter.Should().NotBeNull();
        var bson = RenderFilter(capturedFilter);
        var json = bson.ToJson();

        // Must NOT contain $or (proves & was used, not |=)
        json.Should().NotContain("$or");

        // Verify all fields are present in the filter
        json.Should().Contain("tenantId");
        json.Should().Contain("tenant-all");
        json.Should().Contain("actorId");
        json.Should().Contain("user-1");
        json.Should().Contain("actionCode");
        json.Should().Contain("Create");
        json.Should().Contain("resourceType");
        json.Should().Contain("Order");
        json.Should().Contain("resourceId");
        json.Should().Contain("ord-1");
        json.Should().Contain("outcome");
        json.Should().Contain("occurredAt");
        json.Should().Contain("$gte");
        json.Should().Contain("$lte");
        json.Should().Contain("correlationId");
        json.Should().Contain("corr-1");
        json.Should().Contain("_id");
        json.Should().Contain("$gt");

        // Limit must be PageSize + 1 (26)
        capturedOptions.Should().NotBeNull();
        capturedOptions.Limit.Should().Be(26);
    }

    [Theory]
    [InlineData("ActorId", "usr-1")]
    [InlineData("ActionCode", "Delete")]
    [InlineData("ResourceType", "Account")]
    [InlineData("ResourceId", "acc-99")]
    [InlineData("CorrelationId", "c-100")]
    public async Task QueryAsync_StringFilters_IncludedWhenSet_ExcludedWhenWhitespace(string propertyName, string testValue)
    {
        var collection = Substitute.For<IMongoCollection<MongoAuditRecordDocument>>();
        var store = new MongoAuditStore(collection);

        FilterDefinition<MongoAuditRecordDocument> capturedFilter = null!;
        var cursor = Substitute.For<IAsyncCursor<MongoAuditRecordDocument>>();
        cursor.MoveNextAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(true), Task.FromResult(false));
        cursor.Current.Returns(new List<MongoAuditRecordDocument>());

        collection.FindAsync(
            Arg.Do<FilterDefinition<MongoAuditRecordDocument>>(f => capturedFilter = f),
            Arg.Any<FindOptions<MongoAuditRecordDocument, MongoAuditRecordDocument>>(),
            Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(cursor));

        string elementName = char.ToLowerInvariant(propertyName[0]) + propertyName.Substring(1);

        // 1. When set with value
        var qSet = new AuditQuery { TenantId = "t1" };
        typeof(AuditQuery).GetProperty(propertyName)!.SetValue(qSet, testValue);
        await store.QueryAsync(qSet);
        var jsonSet = RenderFilter(capturedFilter).ToJson();
        jsonSet.Should().Contain(elementName);
        jsonSet.Should().Contain(testValue);
        jsonSet.Should().NotContain("$or");

        // 2. When null
        var qNull = new AuditQuery { TenantId = "t1" };
        typeof(AuditQuery).GetProperty(propertyName)!.SetValue(qNull, null);
        await store.QueryAsync(qNull);
        var jsonNull = RenderFilter(capturedFilter).ToJson();
        jsonNull.Should().NotContain(elementName);

        // 3. When empty
        var qEmpty = new AuditQuery { TenantId = "t1" };
        typeof(AuditQuery).GetProperty(propertyName)!.SetValue(qEmpty, "");
        await store.QueryAsync(qEmpty);
        var jsonEmpty = RenderFilter(capturedFilter).ToJson();
        jsonEmpty.Should().NotContain(elementName);

        // 4. When whitespace
        var qWhitespace = new AuditQuery { TenantId = "t1" };
        typeof(AuditQuery).GetProperty(propertyName)!.SetValue(qWhitespace, "   ");
        await store.QueryAsync(qWhitespace);
        var jsonWhitespace = RenderFilter(capturedFilter).ToJson();
        jsonWhitespace.Should().NotContain(elementName);
    }

    [Fact]
    public async Task QueryAsync_OutcomeFilter_IncludedWhenSet_ExcludedWhenNull()
    {
        var collection = Substitute.For<IMongoCollection<MongoAuditRecordDocument>>();
        var store = new MongoAuditStore(collection);

        FilterDefinition<MongoAuditRecordDocument> capturedFilter = null!;
        var cursor = Substitute.For<IAsyncCursor<MongoAuditRecordDocument>>();
        cursor.MoveNextAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(true), Task.FromResult(false));
        cursor.Current.Returns(new List<MongoAuditRecordDocument>());

        collection.FindAsync(
            Arg.Do<FilterDefinition<MongoAuditRecordDocument>>(f => capturedFilter = f),
            Arg.Any<FindOptions<MongoAuditRecordDocument, MongoAuditRecordDocument>>(),
            Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(cursor));

        // When set
        await store.QueryAsync(new AuditQuery { TenantId = "t1", Outcome = AuditOutcome.Failure });
        var jsonSet = RenderFilter(capturedFilter).ToJson();
        jsonSet.Should().Contain("outcome");
        jsonSet.Should().Contain("1");
        jsonSet.Should().NotContain("$or");

        // When null
        await store.QueryAsync(new AuditQuery { TenantId = "t1", Outcome = null });
        var jsonNull = RenderFilter(capturedFilter).ToJson();
        jsonNull.Should().NotContain("outcome");
    }

    [Fact]
    public async Task QueryAsync_DateFilters_IncludedWhenSet_ExcludedWhenNull()
    {
        var collection = Substitute.For<IMongoCollection<MongoAuditRecordDocument>>();
        var store = new MongoAuditStore(collection);

        FilterDefinition<MongoAuditRecordDocument> capturedFilter = null!;
        var cursor = Substitute.For<IAsyncCursor<MongoAuditRecordDocument>>();
        cursor.MoveNextAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(true), Task.FromResult(false));
        cursor.Current.Returns(new List<MongoAuditRecordDocument>());

        collection.FindAsync(
            Arg.Do<FilterDefinition<MongoAuditRecordDocument>>(f => capturedFilter = f),
            Arg.Any<FindOptions<MongoAuditRecordDocument, MongoAuditRecordDocument>>(),
            Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(cursor));

        var date = DateTimeOffset.UtcNow;

        // From only
        await store.QueryAsync(new AuditQuery { TenantId = "t1", From = date });
        var jsonFrom = RenderFilter(capturedFilter).ToJson();
        jsonFrom.Should().Contain("occurredAt");
        jsonFrom.Should().Contain("$gte");
        jsonFrom.Should().NotContain("$lte");
        jsonFrom.Should().NotContain("$or");

        // To only
        await store.QueryAsync(new AuditQuery { TenantId = "t1", To = date });
        var jsonTo = RenderFilter(capturedFilter).ToJson();
        jsonTo.Should().Contain("occurredAt");
        jsonTo.Should().Contain("$lte");
        jsonTo.Should().NotContain("$gte");
        jsonTo.Should().NotContain("$or");

        // Neither
        await store.QueryAsync(new AuditQuery { TenantId = "t1", From = null, To = null });
        var jsonNeither = RenderFilter(capturedFilter).ToJson();
        jsonNeither.Should().NotContain("occurredAt");
    }

    [Fact]
    public async Task QueryAsync_AfterRecordIdFilter_IncludedWhenSet_ExcludedWhenNull()
    {
        var collection = Substitute.For<IMongoCollection<MongoAuditRecordDocument>>();
        var store = new MongoAuditStore(collection);

        FilterDefinition<MongoAuditRecordDocument> capturedFilter = null!;
        var cursor = Substitute.For<IAsyncCursor<MongoAuditRecordDocument>>();
        cursor.MoveNextAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(true), Task.FromResult(false));
        cursor.Current.Returns(new List<MongoAuditRecordDocument>());

        collection.FindAsync(
            Arg.Do<FilterDefinition<MongoAuditRecordDocument>>(f => capturedFilter = f),
            Arg.Any<FindOptions<MongoAuditRecordDocument, MongoAuditRecordDocument>>(),
            Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(cursor));

        var afterId = Guid.NewGuid();

        // When set
        await store.QueryAsync(new AuditQuery { TenantId = "t1", AfterRecordId = afterId });
        var jsonSet = RenderFilter(capturedFilter).ToJson();
        jsonSet.Should().Contain("_id");
        jsonSet.Should().Contain("$gt");
        jsonSet.Should().NotContain("$or");

        // When null
        await store.QueryAsync(new AuditQuery { TenantId = "t1", AfterRecordId = null });
        var jsonNull = RenderFilter(capturedFilter).ToJson();
        jsonNull.Should().NotContain("_id");
    }

    [Fact]
    public async Task QueryAsync_Pagination_DocsCountEqualsPageSize_HasMoreIsFalse()
    {
        var collection = Substitute.For<IMongoCollection<MongoAuditRecordDocument>>();
        var store = new MongoAuditStore(collection);

        var doc1 = new MongoAuditRecordDocument { Id = Guid.NewGuid(), TenantId = "t1", ActionCode = "A1", ActorId = "u1", ResourceId = "r1", ResourceType = "R1", Source = "S1" };
        var doc2 = new MongoAuditRecordDocument { Id = Guid.NewGuid(), TenantId = "t1", ActionCode = "A2", ActorId = "u1", ResourceId = "r2", ResourceType = "R1", Source = "S1" };
        var doc3 = new MongoAuditRecordDocument { Id = Guid.NewGuid(), TenantId = "t1", ActionCode = "A3", ActorId = "u1", ResourceId = "r3", ResourceType = "R1", Source = "S1" };

        var cursor = Substitute.For<IAsyncCursor<MongoAuditRecordDocument>>();
        cursor.MoveNextAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(true), Task.FromResult(false));
        cursor.Current.Returns(new List<MongoAuditRecordDocument> { doc1, doc2, doc3 });

        collection.FindAsync(
            Arg.Any<FilterDefinition<MongoAuditRecordDocument>>(),
            Arg.Any<FindOptions<MongoAuditRecordDocument, MongoAuditRecordDocument>>(),
            Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(cursor));

        var result = await store.QueryAsync(new AuditQuery { TenantId = "t1", PageSize = 3 });

        result.Records.Should().HaveCount(3);
        result.HasMore.Should().BeFalse();
        result.NextCursorId.Should().BeNull();
    }

    [Fact]
    public async Task QueryAsync_Pagination_DocsCountGreaterThanPageSize_HasMoreIsTrue()
    {
        var collection = Substitute.For<IMongoCollection<MongoAuditRecordDocument>>();
        var store = new MongoAuditStore(collection);

        var doc1 = new MongoAuditRecordDocument { Id = Guid.NewGuid(), TenantId = "t1", ActionCode = "A1", ActorId = "u1", ResourceId = "r1", ResourceType = "R1", Source = "S1" };
        var doc2 = new MongoAuditRecordDocument { Id = Guid.NewGuid(), TenantId = "t1", ActionCode = "A2", ActorId = "u1", ResourceId = "r2", ResourceType = "R1", Source = "S1" };
        var doc3 = new MongoAuditRecordDocument { Id = Guid.NewGuid(), TenantId = "t1", ActionCode = "A3", ActorId = "u1", ResourceId = "r3", ResourceType = "R1", Source = "S1" };
        var doc4 = new MongoAuditRecordDocument { Id = Guid.NewGuid(), TenantId = "t1", ActionCode = "A4", ActorId = "u1", ResourceId = "r4", ResourceType = "R1", Source = "S1" };

        var cursor = Substitute.For<IAsyncCursor<MongoAuditRecordDocument>>();
        cursor.MoveNextAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(true), Task.FromResult(false));
        cursor.Current.Returns(new List<MongoAuditRecordDocument> { doc1, doc2, doc3, doc4 });

        collection.FindAsync(
            Arg.Any<FilterDefinition<MongoAuditRecordDocument>>(),
            Arg.Any<FindOptions<MongoAuditRecordDocument, MongoAuditRecordDocument>>(),
            Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(cursor));

        var result = await store.QueryAsync(new AuditQuery { TenantId = "t1", PageSize = 3 });

        result.Records.Should().HaveCount(3);
        result.HasMore.Should().BeTrue();
        result.NextCursorId.Should().Be(doc3.Id);
    }

    [Fact]
    public async Task QueryAsync_DocumentMapping_IncludesAllFieldsAndHandlesChanges()
    {
        var collection = Substitute.For<IMongoCollection<MongoAuditRecordDocument>>();
        var store = new MongoAuditStore(collection);

        var docWithChanges = new MongoAuditRecordDocument
        {
            Id = Guid.NewGuid(),
            TenantId = "tenant-map",
            OccurredAt = DateTime.UtcNow,
            Source = "App",
            ActorType = (byte)AuditActorType.User,
            ActorId = "user-1",
            ActorName = "Alice",
            ActionCode = "Create",
            ResourceType = "Order",
            ResourceId = "ord-1",
            AggregateType = "Account",
            AggregateId = "acc-1",
            Outcome = (byte)AuditOutcome.Success,
            ErrorCode = "0",
            CorrelationId = "corr-1",
            CausationId = "cause-1",
            RequestId = "req-1",
            IpAddress = "127.0.0.1",
            UserAgent = "TestAgent",
            Changes = new List<MongoAuditChangeDocument>
            {
                new() { Field = "Status", OldValue = "Draft", NewValue = "Active", IsRedacted = false }
            },
            IntegrityHash = "hash-1",
            PreviousHash = "prev-0"
        };

        var docWithEmptyChanges = new MongoAuditRecordDocument
        {
            Id = Guid.NewGuid(),
            TenantId = "tenant-map",
            OccurredAt = DateTime.UtcNow,
            Source = "App",
            ActorType = (byte)AuditActorType.Service,
            ActorId = "svc-1",
            ActionCode = "Update",
            ResourceType = "Order",
            ResourceId = "ord-2",
            Outcome = (byte)AuditOutcome.Failure,
            Changes = new List<MongoAuditChangeDocument>() // Empty changes
        };

        var cursor = Substitute.For<IAsyncCursor<MongoAuditRecordDocument>>();
        cursor.MoveNextAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(true), Task.FromResult(false));
        cursor.Current.Returns(new List<MongoAuditRecordDocument> { docWithChanges, docWithEmptyChanges });

        collection.FindAsync(
            Arg.Any<FilterDefinition<MongoAuditRecordDocument>>(),
            Arg.Any<FindOptions<MongoAuditRecordDocument, MongoAuditRecordDocument>>(),
            Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(cursor));

        var result = await store.QueryAsync(new AuditQuery { TenantId = "tenant-map", PageSize = 10 });

        result.Records.Should().HaveCount(2);

        var r1 = result.Records[0];
        r1.Id.Should().Be(docWithChanges.Id);
        r1.Context.TenantId.Should().Be("tenant-map");
        r1.Context.Source.Should().Be("App");
        r1.Actor.Type.Should().Be(AuditActorType.User);
        r1.Actor.Id.Should().Be("user-1");
        r1.Actor.DisplayName.Should().Be("Alice");
        r1.Action.Code.Should().Be("Create");
        r1.Resource.Type.Should().Be("Order");
        r1.Resource.Id.Should().Be("ord-1");
        r1.Resource.AggregateType.Should().Be("Account");
        r1.Resource.AggregateId.Should().Be("acc-1");
        r1.Outcome.Should().Be(AuditOutcome.Success);
        r1.ErrorCode.Should().Be("0");
        r1.Context.CorrelationId.Should().Be("corr-1");
        r1.Context.CausationId.Should().Be("cause-1");
        r1.Context.RequestId.Should().Be("req-1");
        r1.Context.IpAddress.Should().Be("127.0.0.1");
        r1.Context.UserAgent.Should().Be("TestAgent");
        r1.IntegrityHash.Should().Be("hash-1");
        r1.PreviousHash.Should().Be("prev-0");
        r1.Changes.Should().NotBeNull();
        r1.Changes!.Count.Should().Be(1);
        r1.Changes[0].Field.Should().Be("Status");
        r1.Changes[0].OldValue.Should().Be("Draft");
        r1.Changes[0].NewValue.Should().Be("Active");
        r1.Changes[0].IsRedacted.Should().BeFalse();

        var r2 = result.Records[1];
        r2.Id.Should().Be(docWithEmptyChanges.Id);
        r2.Changes.Should().BeNull(); // Empty changes maps to null
    }

    [Fact]
    public async Task QueryAsync_PageSizeClamping_Works()
    {
        var collection = Substitute.For<IMongoCollection<MongoAuditRecordDocument>>();
        var store = new MongoAuditStore(collection);

        FindOptions<MongoAuditRecordDocument, MongoAuditRecordDocument> capturedOptions = null!;

        collection.FindAsync(
            Arg.Any<FilterDefinition<MongoAuditRecordDocument>>(),
            Arg.Do<FindOptions<MongoAuditRecordDocument, MongoAuditRecordDocument>>(o => capturedOptions = o),
            Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                var cursor = Substitute.For<IAsyncCursor<MongoAuditRecordDocument>>();
                cursor.MoveNextAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(true), Task.FromResult(false));
                cursor.Current.Returns(new List<MongoAuditRecordDocument>());
                return Task.FromResult(cursor);
            });

        // PageSize = 0 clamps to 1 -> Limit = 2
        await store.QueryAsync(new AuditQuery { TenantId = "tenant-clamp", PageSize = 0 });
        capturedOptions.Limit.Should().Be(2);

        // PageSize = -5 clamps to 1 -> Limit = 2
        await store.QueryAsync(new AuditQuery { TenantId = "tenant-clamp", PageSize = -5 });
        capturedOptions.Limit.Should().Be(2);

        // PageSize = 2000 clamps to 1000 -> Limit = 1001
        await store.QueryAsync(new AuditQuery { TenantId = "tenant-clamp", PageSize = 2000 });
        capturedOptions.Limit.Should().Be(1001);
    }

    [Fact]
    public void AddMongoDbAuditStore_RegistersServicesCorrectly()
    {
        var services = new ServiceCollection();
        var fakeDb = Substitute.For<IMongoDatabase>();
        var fakeCollection = Substitute.For<IMongoCollection<MongoAuditRecordDocument>>();
        fakeDb.GetCollection<MongoAuditRecordDocument>("custom_audit_col").Returns(fakeCollection);

        services.AddMongoDbAuditStore(
            _ => fakeDb,
            options =>
            {
                options.CollectionName = "custom_audit_col";
                options.DatabaseName = "custom_db";
            });

        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<MongoAuditStoreOptions>();
        options.CollectionName.Should().Be("custom_audit_col");
        options.DatabaseName.Should().Be("custom_db");

        var store = provider.GetService<IAuditStore>();
        store.Should().NotBeNull();
        store.Should().BeOfType<MongoAuditStore>();
    }

    [Fact]
    public void AddMongoDbAuditStore_WithoutConfigure_UsesDefaults()
    {
        var services = new ServiceCollection();
        var fakeDb = Substitute.For<IMongoDatabase>();
        var fakeCollection = Substitute.For<IMongoCollection<MongoAuditRecordDocument>>();
        fakeDb.GetCollection<MongoAuditRecordDocument>("audit_records").Returns(fakeCollection);

        services.AddMongoDbAuditStore(_ => fakeDb);

        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<MongoAuditStoreOptions>();
        options.CollectionName.Should().Be("audit_records");
        options.DatabaseName.Should().Be("AuditingDb");

        var store = provider.GetService<IAuditStore>();
        store.Should().NotBeNull();
        store.Should().BeOfType<MongoAuditStore>();
    }

    [Fact]
    public void AddMongoDbAuditStore_NullGuards_ThrowWithParamName()
    {
        var configureCalled = false;
        IServiceCollection services = null!;
        var act1 = () => services.AddMongoDbAuditStore(
            _ => Substitute.For<IMongoDatabase>(),
            _ => { configureCalled = true; });

        act1.Should().Throw<ArgumentNullException>().WithParameterName("services");
        configureCalled.Should().BeFalse();

        var validServices = new ServiceCollection();
        var act2 = () => validServices.AddMongoDbAuditStore(null!);
        act2.Should().Throw<ArgumentNullException>().WithParameterName("databaseFactory");
    }
}
