// Copyright © Erickson Lopez. MIT License.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AwesomeAssertions;
using EricksonLopez.Auditing;
using EricksonLopez.Auditing.MongoDb;
using EricksonLopez.Auditing.Testing;
using MongoDB.Driver;
using Testcontainers.MongoDb;
using Xunit;

namespace EricksonLopez.Auditing.IntegrationTests;

public sealed class MongoAuditStoreIntegrationTests : IAsyncLifetime
{
    private readonly MongoDbContainer _container = new MongoDbBuilder("mongo:7.0")
        .Build();

    private IMongoDatabase _database = null!;
    private MongoAuditStore _store = null!;
    private IMongoCollection<MongoAuditRecordDocument> _collection = null!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        var client = new MongoClient(_container.GetConnectionString());
        _database = client.GetDatabase("test_db");

        var options = new MongoAuditStoreOptions { CollectionName = "audit_records" };
        _collection = _database.GetCollection<MongoAuditRecordDocument>(options.CollectionName);
        _store = new MongoAuditStore(_database, options);
    }

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }

    [Fact(Timeout = 30000)]
    public async Task AppendAndQuery_WithFilters_ReturnsMatches()
    {
        var tenant = "tenant-mongo";
        var record1 = AuditRecordBuilder.BuildDefault(tenantId: tenant, actorId: "usr-1", resourceType: "Order", resourceId: "ord-1");
        var record2 = AuditRecordBuilder.BuildDefault(tenantId: tenant, actorId: "usr-1", resourceType: "Order", resourceId: "ord-2");
        var record3 = AuditRecordBuilder.BuildDefault(tenantId: tenant, actorId: "usr-2", resourceType: "Invoice", resourceId: "inv-1");

        await _store.AppendBatchAsync(new[] { record1, record2, record3 });

        var query = new AuditQuery
        {
            TenantId = tenant,
            ActorId = "usr-1",
            ResourceType = "Order"
        };

        var result = await _store.QueryAsync(query);

        result.Records.Should().HaveCount(2);
        result.Records.Select(r => r.Id).Should().Contain(new[] { record1.Id, record2.Id });
        result.HasMore.Should().BeFalse();
    }

    [Fact(Timeout = 30000)]
    public async Task QueryAsync_KeysetPagination_PagesCorrectly()
    {
        var tenant = "tenant-mongo-page";
        var records = new List<AuditRecord>();
        for (int i = 0; i < 5; i++)
        {
            records.Add(AuditRecordBuilder.BuildDefault(tenantId: tenant));
            await Task.Delay(1);
        }

        records = records.OrderBy(r => r.Id).ToList();
        await _store.AppendBatchAsync(records);

        var query = new AuditQuery { TenantId = tenant, PageSize = 2 };
        var page1 = await _store.QueryAsync(query);

        page1.Records.Should().HaveCount(2);
        page1.HasMore.Should().BeTrue();
        page1.NextCursorId.Should().Be(records[1].Id);

        var query2 = new AuditQuery { TenantId = tenant, PageSize = 2, AfterRecordId = page1.NextCursorId };
        var page2 = await _store.QueryAsync(query2);

        page2.Records.Should().HaveCount(2);
        page2.HasMore.Should().BeTrue();
        page2.NextCursorId.Should().Be(records[3].Id);

        var query3 = new AuditQuery { TenantId = tenant, PageSize = 2, AfterRecordId = page2.NextCursorId };
        var page3 = await _store.QueryAsync(query3);

        page3.Records.Should().HaveCount(1);
        page3.HasMore.Should().BeFalse();
        page3.NextCursorId.Should().BeNull();
    }
}
