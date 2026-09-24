// Copyright © Erickson Lopez. MIT License.
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using Xunit;

namespace EricksonLopez.Auditing.Abstractions.Tests;

public sealed class AuditStoreStreamAsyncTests
{
    private sealed class FakeAuditStore : IAuditStore
    {
        private readonly Queue<AuditQueryResult> _pages;
        private readonly int _maxQueries;
        public List<AuditQuery> RecordedQueries { get; } = new();
        public Action? OnQueryCallback { get; set; }

        public FakeAuditStore(params AuditQueryResult[] pages)
        {
            _pages = new Queue<AuditQueryResult>(pages);
            _maxQueries = pages.Length > 0 ? pages.Length : 1;
        }

        public ValueTask AppendAsync(AuditRecord record, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public ValueTask AppendBatchAsync(IReadOnlyList<AuditRecord> records, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        public ValueTask<AuditQueryResult> QueryAsync(AuditQuery query, CancellationToken cancellationToken = default)
        {
            if (RecordedQueries.Count >= _maxQueries)
            {
                throw new InvalidOperationException($"Infinite loop detected: QueryAsync called {RecordedQueries.Count + 1} times, exceeding expected max {_maxQueries}");
            }

            RecordedQueries.Add(query);
            OnQueryCallback?.Invoke();

            if (_pages.Count > 0)
            {
                return ValueTask.FromResult(_pages.Dequeue());
            }

            return ValueTask.FromResult(new AuditQueryResult(Array.Empty<AuditRecord>(), null, false));
        }
    }

    private static AuditRecord CreateRecord(string actionCode = "Read") => new()
    {
        Id = Guid.NewGuid(),
        OccurredAt = DateTimeOffset.UtcNow,
        Actor = AuditActor.Anonymous,
        Action = new AuditAction(actionCode),
        Resource = new AuditResource("User", "123"),
        Outcome = AuditOutcome.Success,
        Context = new AuditContext(new TenantId("tenant-1"), "Web")
    };

    [Fact]
    public async Task StreamAsync_WhenCancellationAlreadyRequested_DoesNotQueryAndYieldsZero()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var record = CreateRecord();
        var page = new AuditQueryResult(new[] { record }, null, false);
        var store = new FakeAuditStore(page);

        var query = new AuditQuery { TenantId = new TenantId("tenant-1") };
        var streamed = new List<AuditRecord>();

        await foreach (var item in ((IAuditStore)store).StreamAsync(query, cts.Token))
        {
            streamed.Add(item);
        }

        streamed.Should().BeEmpty();
        store.RecordedQueries.Should().BeEmpty();
    }

    [Fact]
    public async Task StreamAsync_WhenCancelledDuringStreaming_StopsBeforeNextPage()
    {
        using var cts = new CancellationTokenSource();

        var record1 = CreateRecord("Action1");
        var record2 = CreateRecord("Action2");

        var page1 = new AuditQueryResult(new[] { record1 }, "token-page-2", true);
        var page2 = new AuditQueryResult(new[] { record2 }, null, false);
        var store = new FakeAuditStore(page1, page2)
        {
            OnQueryCallback = () => cts.Cancel()
        };

        var query = new AuditQuery { TenantId = new TenantId("tenant-1") };
        var streamed = new List<AuditRecord>();

        await foreach (var item in ((IAuditStore)store).StreamAsync(query, cts.Token))
        {
            streamed.Add(item);
        }

        streamed.Should().HaveCount(1);
        streamed[0].Id.Should().Be(record1.Id);
        store.RecordedQueries.Should().HaveCount(1);
    }

    [Fact]
    public async Task StreamAsync_SinglePageWithNullToken_YieldsRecordsAndTerminates()
    {
        var record1 = CreateRecord("Action1");
        var record2 = CreateRecord("Action2");
        var page = new AuditQueryResult(new[] { record1, record2 }, null, false);
        var store = new FakeAuditStore(page);

        var query = new AuditQuery { TenantId = new TenantId("tenant-1"), ActorId = "actor-99" };
        var streamed = new List<AuditRecord>();

        await foreach (var item in ((IAuditStore)store).StreamAsync(query))
        {
            streamed.Add(item);
        }

        streamed.Should().HaveCount(2);
        streamed[0].Id.Should().Be(record1.Id);
        streamed[1].Id.Should().Be(record2.Id);
        store.RecordedQueries.Should().HaveCount(1);
        store.RecordedQueries[0].ContinuationToken.Should().BeNull();
        store.RecordedQueries[0].ActorId.Should().Be("actor-99");
    }

    [Fact]
    public async Task StreamAsync_SinglePageWithEmptyStringToken_YieldsRecordsAndTerminates()
    {
        var record = CreateRecord();
        var page = new AuditQueryResult(new[] { record }, string.Empty, false);
        var store = new FakeAuditStore(page);

        var query = new AuditQuery { TenantId = new TenantId("tenant-1") };
        var streamed = new List<AuditRecord>();

        await foreach (var item in ((IAuditStore)store).StreamAsync(query))
        {
            streamed.Add(item);
        }

        streamed.Should().HaveCount(1);
        streamed[0].Id.Should().Be(record.Id);
        store.RecordedQueries.Should().HaveCount(1);
    }

    [Fact]
    public async Task StreamAsync_MultiplePages_PassesContinuationTokenAndStreamsAll()
    {
        var record1 = CreateRecord("Action1");
        var record2 = CreateRecord("Action2");
        var record3 = CreateRecord("Action3");

        var page1 = new AuditQueryResult(new[] { record1 }, "token-page-2", true);
        var page2 = new AuditQueryResult(new[] { record2, record3 }, null, false);
        var store = new FakeAuditStore(page1, page2);

        var initialQuery = new AuditQuery { TenantId = new TenantId("tenant-1"), ActionCode = "FilterMe" };
        var streamed = new List<AuditRecord>();

        await foreach (var item in ((IAuditStore)store).StreamAsync(initialQuery))
        {
            streamed.Add(item);
        }

        streamed.Should().HaveCount(3);
        streamed[0].Id.Should().Be(record1.Id);
        streamed[1].Id.Should().Be(record2.Id);
        streamed[2].Id.Should().Be(record3.Id);

        store.RecordedQueries.Should().HaveCount(2);
        store.RecordedQueries[0].ContinuationToken.Should().BeNull();
        store.RecordedQueries[0].ActionCode.Should().Be("FilterMe");
        store.RecordedQueries[1].ContinuationToken.Should().Be("token-page-2");
        store.RecordedQueries[1].ActionCode.Should().Be("FilterMe");
    }
}
