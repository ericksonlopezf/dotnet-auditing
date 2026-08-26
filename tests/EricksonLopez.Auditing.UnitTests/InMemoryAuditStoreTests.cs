// Copyright © Erickson Lopez. MIT License.
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using EricksonLopez.Auditing.Testing;
using Xunit;

namespace EricksonLopez.Auditing.UnitTests;

public sealed class InMemoryAuditStoreTests
{
    [Fact]
    public async Task AppendAsync_SingleRecord_IsStored()
    {
        var store = new InMemoryAuditStore();
        var record = AuditRecordBuilder.BuildDefault();

        await store.AppendAsync(record);

        store.Count.Should().Be(1);
        store.Records[0].Id.Should().Be(record.Id);
    }

    [Fact]
    public async Task AppendBatchAsync_MultipleRecords_AllStored()
    {
        var store = new InMemoryAuditStore();
        var records = new[]
        {
            AuditRecordBuilder.BuildDefault(resourceId: "1"),
            AuditRecordBuilder.BuildDefault(resourceId: "2"),
            AuditRecordBuilder.BuildDefault(resourceId: "3")
        };

        await store.AppendBatchAsync(records);

        store.Count.Should().Be(3);
    }

    [Fact]
    public async Task ForTenant_ReturnsOnlyMatchingTenantRecords()
    {
        var store = new InMemoryAuditStore();
        await store.AppendAsync(AuditRecordBuilder.BuildDefault(tenantId: "tenant-a"));
        await store.AppendAsync(AuditRecordBuilder.BuildDefault(tenantId: "tenant-b"));
        await store.AppendAsync(AuditRecordBuilder.BuildDefault(tenantId: "tenant-a"));

        var tenantA = store.ForTenant("tenant-a");

        tenantA.Should().HaveCount(2);
        tenantA.Should().AllSatisfy(r => r.Context.TenantId.Should().Be("tenant-a"));
    }

    [Fact]
    public async Task TenantA_CannotSee_TenantB_Records_ViaQuery()
    {
        var store = new InMemoryAuditStore();
        await store.AppendAsync(AuditRecordBuilder.BuildDefault(tenantId: "tenant-a", resourceId: "res-1"));
        await store.AppendAsync(AuditRecordBuilder.BuildDefault(tenantId: "tenant-b", resourceId: "res-2"));

        var result = await store.QueryAsync(new AuditQuery
        {
            TenantId = "tenant-a",
            PageSize = 10
        });

        result.Records.Should().HaveCount(1);
        result.Records.Should().AllSatisfy(r =>
            r.Context.TenantId.Should().Be("tenant-a"),
            "Tenant A query must never return Tenant B records");
    }

    [Fact]
    public async Task QueryAsync_FilterByActorId_ReturnsMatchingOnly()
    {
        var store = new InMemoryAuditStore();
        await store.AppendAsync(AuditRecordBuilder.BuildDefault(actorId: "alice"));
        await store.AppendAsync(AuditRecordBuilder.BuildDefault(actorId: "bob"));
        await store.AppendAsync(AuditRecordBuilder.BuildDefault(actorId: "alice"));

        var result = await store.QueryAsync(new AuditQuery
        {
            TenantId = "tenant-a",
            ActorId = "alice",
            PageSize = 10
        });

        result.Records.Should().HaveCount(2);
        result.Records.Should().AllSatisfy(r => r.Actor.Id.Should().Be("alice"));
    }

    [Fact]
    public async Task QueryAsync_FilterByOutcome_ReturnsMatchingOnly()
    {
        var store = new InMemoryAuditStore();
        await store.AppendAsync(AuditRecordBuilder.BuildDefault(outcome: AuditOutcome.Success));
        await store.AppendAsync(AuditRecordBuilder.BuildDefault(outcome: AuditOutcome.Denied));
        await store.AppendAsync(AuditRecordBuilder.BuildDefault(outcome: AuditOutcome.Failure));

        var result = await store.QueryAsync(new AuditQuery
        {
            TenantId = "tenant-a",
            Outcome = AuditOutcome.Denied,
            PageSize = 10
        });

        result.Records.Should().HaveCount(1);
        result.Records[0].Outcome.Should().Be(AuditOutcome.Denied);
    }

    [Fact]
    public async Task QueryAsync_KeysetPagination_ReturnsCorrectPage()
    {
        var store = new InMemoryAuditStore();
        for (int i = 0; i < 5; i++)
        {
            await store.AppendAsync(AuditRecordBuilder.BuildDefault(resourceId: $"res-{i}"));
        }

        // First page of 2
        var page1 = await store.QueryAsync(new AuditQuery { TenantId = "tenant-a", PageSize = 2 });
        page1.Records.Should().HaveCount(2);
        page1.HasMore.Should().BeTrue();
        page1.NextCursorId.Should().NotBeNull();

        // Second page
        var page2 = await store.QueryAsync(new AuditQuery
        {
            TenantId = "tenant-a",
            PageSize = 2,
            AfterRecordId = page1.NextCursorId
        });
        page2.Records.Should().HaveCount(2);

        // Third page (last)
        var page3 = await store.QueryAsync(new AuditQuery
        {
            TenantId = "tenant-a",
            PageSize = 2,
            AfterRecordId = page2.NextCursorId
        });
        page3.Records.Should().HaveCount(1);
        page3.HasMore.Should().BeFalse();
        page3.NextCursorId.Should().BeNull();
    }

    [Fact]
    public async Task ForActor_ReturnsOnlyMatchingActorRecords()
    {
        var store = new InMemoryAuditStore();
        await store.AppendAsync(AuditRecordBuilder.BuildDefault(actorId: "actor-1"));
        await store.AppendAsync(AuditRecordBuilder.BuildDefault(actorId: "actor-2"));
        await store.AppendAsync(AuditRecordBuilder.BuildDefault(actorId: "actor-1"));

        var actorRecords = store.ForActor("actor-1");

        actorRecords.Should().HaveCount(2);
        actorRecords.Should().AllSatisfy(r => r.Actor.Id.Should().Be("actor-1"));
    }

    [Fact]
    public async Task QueryAsync_FilterByActionCode_ReturnsMatchingOnly()
    {
        var store = new InMemoryAuditStore();
        await store.AppendAsync(AuditRecordBuilder.BuildDefault() with { Action = AuditAction.Create });
        await store.AppendAsync(AuditRecordBuilder.BuildDefault() with { Action = AuditAction.Delete });
        await store.AppendAsync(AuditRecordBuilder.BuildDefault() with { Action = AuditAction.Create });

        var result = await store.QueryAsync(new AuditQuery
        {
            TenantId = "tenant-a",
            ActionCode = "Create"
        });

        result.Records.Should().HaveCount(2);
        result.Records.Should().AllSatisfy(r => r.Action.Code.Should().Be("Create"));
    }

    [Fact]
    public async Task QueryAsync_FilterByResourceTypeAndId_ReturnsMatchingOnly()
    {
        var store = new InMemoryAuditStore();
        await store.AppendAsync(AuditRecordBuilder.BuildDefault(resourceType: "Invoice", resourceId: "inv-100"));
        await store.AppendAsync(AuditRecordBuilder.BuildDefault(resourceType: "Order", resourceId: "ord-200"));
        await store.AppendAsync(AuditRecordBuilder.BuildDefault(resourceType: "Invoice", resourceId: "inv-300"));

        var typeResult = await store.QueryAsync(new AuditQuery
        {
            TenantId = "tenant-a",
            ResourceType = "Invoice"
        });
        typeResult.Records.Should().HaveCount(2);

        var idResult = await store.QueryAsync(new AuditQuery
        {
            TenantId = "tenant-a",
            ResourceId = "inv-100"
        });
        idResult.Records.Should().HaveCount(1);
        idResult.Records[0].Resource.Id.Should().Be("inv-100");
    }

    [Fact]
    public async Task QueryAsync_FilterByDateRange_ReturnsMatchingOnly()
    {
        var store = new InMemoryAuditStore();
        var now = DateTimeOffset.UtcNow;
        var r1 = AuditRecordBuilder.BuildDefault() with { OccurredAt = now.AddHours(-3) };
        var r2 = AuditRecordBuilder.BuildDefault() with { OccurredAt = now.AddHours(-1) };
        var r3 = AuditRecordBuilder.BuildDefault() with { OccurredAt = now.AddHours(1) };

        await store.AppendBatchAsync(new[] { r1, r2, r3 });

        var result = await store.QueryAsync(new AuditQuery
        {
            TenantId = "tenant-a",
            From = now.AddHours(-2),
            To = now
        });

        result.Records.Should().HaveCount(1);
        result.Records[0].Id.Should().Be(r2.Id);
    }

    [Fact]
    public async Task QueryAsync_FilterByCorrelationId_ReturnsMatchingOnly()
    {
        var store = new InMemoryAuditStore();
        await store.AppendAsync(AuditRecordBuilder.BuildDefault(correlationId: "corr-1"));
        await store.AppendAsync(AuditRecordBuilder.BuildDefault(correlationId: "corr-2"));

        var result = await store.QueryAsync(new AuditQuery
        {
            TenantId = "tenant-a",
            CorrelationId = "corr-1"
        });

        result.Records.Should().HaveCount(1);
        result.Records[0].Context.CorrelationId.Should().Be("corr-1");
    }

    [Fact]
    public async Task QueryAsync_EmptyStore_ReturnsEmptyResultWithNoCursor()
    {
        var store = new InMemoryAuditStore();
        var result = await store.QueryAsync(new AuditQuery { TenantId = "empty-tenant" });

        result.Records.Should().BeEmpty();
        result.HasMore.Should().BeFalse();
        result.NextCursorId.Should().BeNull();
    }

    [Fact]
    public async Task NullArguments_ThrowArgumentNullException()
    {
        var store = new InMemoryAuditStore();

        Func<Task> appendNull = async () => await store.AppendAsync(null!);
        await appendNull.Should().ThrowAsync<ArgumentNullException>();

        Func<Task> appendBatchNull = async () => await store.AppendBatchAsync(null!);
        await appendBatchNull.Should().ThrowAsync<ArgumentNullException>();

        Func<Task> queryNull = async () => await store.QueryAsync(null!);
        await queryNull.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task CancelledTokens_ThrowOperationCanceledException()
    {
        var store = new InMemoryAuditStore();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Func<Task> append = async () => await store.AppendAsync(AuditRecordBuilder.BuildDefault(), cts.Token);
        await append.Should().ThrowAsync<OperationCanceledException>();

        Func<Task> appendBatch = async () => await store.AppendBatchAsync(new[] { AuditRecordBuilder.BuildDefault() }, cts.Token);
        await appendBatch.Should().ThrowAsync<OperationCanceledException>();

        Func<Task> query = async () => await store.QueryAsync(new AuditQuery { TenantId = "t" }, cts.Token);
        await query.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task QueryAsync_OrdersChronologicallyThenById()
    {
        var store = new InMemoryAuditStore();
        var t0 = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
        var id1 = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var id2 = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var id3 = Guid.Parse("00000000-0000-0000-0000-000000000003");

        // Insert out of order
        var r3 = AuditRecordBuilder.BuildDefault() with { Id = id3, OccurredAt = t0.AddMinutes(5) };
        var r2 = AuditRecordBuilder.BuildDefault() with { Id = id2, OccurredAt = t0 };
        var r1 = AuditRecordBuilder.BuildDefault() with { Id = id1, OccurredAt = t0 };

        await store.AppendAsync(r3);
        await store.AppendAsync(r2);
        await store.AppendAsync(r1);

        var result = await store.QueryAsync(new AuditQuery { TenantId = "tenant-a", PageSize = 10 });
        result.Records.Select(r => r.Id).Should().Equal(id1, id2, id3);
    }

    [Fact]
    public async Task QueryAsync_InclusiveFromBoundary_IncludesRecordAtExactFromDate()
    {
        var store = new InMemoryAuditStore();
        var exactFrom = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
        var r = AuditRecordBuilder.BuildDefault() with { OccurredAt = exactFrom };
        await store.AppendAsync(r);

        var result = await store.QueryAsync(new AuditQuery
        {
            TenantId = "tenant-a",
            From = exactFrom
        });

        result.Records.Should().HaveCount(1);
        result.Records[0].Id.Should().Be(r.Id);
    }

    [Fact]
    public async Task QueryAsync_InclusiveToBoundary_IncludesRecordAtExactToDate()
    {
        var store = new InMemoryAuditStore();
        var exactTo = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
        var r = AuditRecordBuilder.BuildDefault() with { OccurredAt = exactTo };
        await store.AppendAsync(r);

        var result = await store.QueryAsync(new AuditQuery
        {
            TenantId = "tenant-a",
            To = exactTo
        });

        result.Records.Should().HaveCount(1);
        result.Records[0].Id.Should().Be(r.Id);
    }

    [Fact]
    public async Task QueryAsync_ExactPageSizeCount_HasMoreIsFalse()
    {
        var store = new InMemoryAuditStore();
        await store.AppendAsync(AuditRecordBuilder.BuildDefault(resourceId: "1"));
        await store.AppendAsync(AuditRecordBuilder.BuildDefault(resourceId: "2"));

        var result = await store.QueryAsync(new AuditQuery { TenantId = "tenant-a", PageSize = 2 });

        result.Records.Should().HaveCount(2);
        result.HasMore.Should().BeFalse();
        result.NextCursorId.Should().BeNull();
    }

    [Fact]
    public async Task Clear_RemovesAllRecords()
    {
        var store = new InMemoryAuditStore();
        await store.AppendAsync(AuditRecordBuilder.BuildDefault());
        await store.AppendAsync(AuditRecordBuilder.BuildDefault());

        store.Clear();

        store.Count.Should().Be(0);
    }

    [Fact]
    public async Task QueryAsync_UnknownAfterRecordId_ReturnsEmpty()
    {
        var store = new InMemoryAuditStore();
        await store.AppendAsync(AuditRecordBuilder.BuildDefault());

        var result = await store.QueryAsync(new AuditQuery
        {
            TenantId = "tenant-a",
            AfterRecordId = Guid.NewGuid()
        });

        result.Records.Should().BeEmpty();
        result.HasMore.Should().BeFalse();
        result.NextCursorId.Should().BeNull();
    }
}
