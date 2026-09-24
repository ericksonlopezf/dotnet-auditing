// Copyright © Erickson Lopez. MIT License.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using AwesomeAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace EricksonLopez.Auditing.Tests;

public sealed class BufferedAuditStoreTests
{
    private sealed class TrackingStore : IAuditStore, IAsyncDisposable, IDisposable
    {
        public List<AuditRecord> FlushedBatches { get; } = new();
        public List<IReadOnlyList<AuditRecord>> BatchCalls { get; } = new();
        public int AppendBatchCallCount { get; private set; }
        public int FailBatchCount { get; set; }
        public bool IsDisposed { get; private set; }
        public bool IsAsyncDisposed { get; private set; }

        private readonly object _lock = new();

        public ValueTask AppendAsync(AuditRecord record, CancellationToken cancellationToken = default)
        {
            lock (_lock)
            {
                FlushedBatches.Add(record);
            }
            return ValueTask.CompletedTask;
        }

        public ValueTask AppendBatchAsync(IReadOnlyList<AuditRecord> records, CancellationToken cancellationToken = default)
        {
            lock (_lock)
            {
                AppendBatchCallCount++;
                if (AppendBatchCallCount <= FailBatchCount)
                {
                    throw new InvalidOperationException("Simulated inner store failure");
                }

                BatchCalls.Add(records.ToList());
                FlushedBatches.AddRange(records);
            }
            return ValueTask.CompletedTask;
        }

        public ValueTask<AuditQueryResult> QueryAsync(AuditQuery query, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(new AuditQueryResult(Array.Empty<AuditRecord>(), null, false));

        public ValueTask DisposeAsync()
        {
            IsAsyncDisposed = true;
            return ValueTask.CompletedTask;
        }

        public void Dispose()
        {
            IsDisposed = true;
        }
    }

    private sealed class SyncDisposableStore : IAuditStore, IDisposable
    {
        public bool Disposed { get; private set; }
        public ValueTask AppendAsync(AuditRecord record, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public ValueTask AppendBatchAsync(IReadOnlyList<AuditRecord> records, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public ValueTask<AuditQueryResult> QueryAsync(AuditQuery query, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(new AuditQueryResult(Array.Empty<AuditRecord>(), null, false));
        public void Dispose() => Disposed = true;
    }

    private sealed class TestLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, string Message, Exception? Exception)> Logs { get; } = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Logs.Add((logLevel, formatter(state, exception), exception));
        }
    }

    // ── BufferedAuditStoreOptions Tests ──────────────────────────────────────────

    [Fact]
    public void Options_Defaults_AreSetCorrectly()
    {
        var options = new BufferedAuditStoreOptions();
        options.Capacity.Should().Be(10_000);
        options.BatchSize.Should().Be(100);
        options.FlushInterval.Should().Be(TimeSpan.FromMilliseconds(500));
        options.FullMode.Should().Be(BoundedChannelFullMode.Wait);
        options.WorkerCount.Should().Be(1);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void Options_Capacity_InvalidValues_ThrowArgumentOutOfRangeException(int invalidCapacity)
    {
        var options = new BufferedAuditStoreOptions();
        Action act = () => options.Capacity = invalidCapacity;
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("value")
            .WithMessage("*Capacity must be greater than zero.*");
    }

    [Fact]
    public void Options_Capacity_ValidValue_Succeeds()
    {
        var options = new BufferedAuditStoreOptions { Capacity = 500 };
        options.Capacity.Should().Be(500);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-50)]
    public void Options_BatchSize_InvalidValues_ThrowArgumentOutOfRangeException(int invalidBatchSize)
    {
        var options = new BufferedAuditStoreOptions();
        Action act = () => options.BatchSize = invalidBatchSize;
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("value")
            .WithMessage("*BatchSize must be greater than zero.*");
    }

    [Fact]
    public void Options_BatchSize_ValidValue_Succeeds()
    {
        var options = new BufferedAuditStoreOptions { BatchSize = 25 };
        options.BatchSize.Should().Be(25);
    }

    [Fact]
    public void Options_FlushInterval_ZeroOrNegative_ThrowsArgumentOutOfRangeException()
    {
        var options = new BufferedAuditStoreOptions();

        Action act1 = () => options.FlushInterval = TimeSpan.Zero;
        act1.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("value")
            .WithMessage("*FlushInterval must be greater than zero.*");

        Action act2 = () => options.FlushInterval = TimeSpan.FromMilliseconds(-10);
        act2.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("value")
            .WithMessage("*FlushInterval must be greater than zero.*");
    }

    [Fact]
    public void Options_FlushInterval_ValidValue_Succeeds()
    {
        var options = new BufferedAuditStoreOptions { FlushInterval = TimeSpan.FromSeconds(2) };
        options.FlushInterval.Should().Be(TimeSpan.FromSeconds(2));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Options_WorkerCount_InvalidValues_ThrowArgumentOutOfRangeException(int invalidWorkerCount)
    {
        var options = new BufferedAuditStoreOptions();
        Action act = () => options.WorkerCount = invalidWorkerCount;
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("value")
            .WithMessage("*WorkerCount must be greater than zero.*");
    }

    [Fact]
    public void Options_WorkerCount_ValidValue_Succeeds()
    {
        var options = new BufferedAuditStoreOptions { WorkerCount = 4 };
        options.WorkerCount.Should().Be(4);
    }

    [Fact]
    public void Options_FullMode_CanBeUpdated()
    {
        var options = new BufferedAuditStoreOptions { FullMode = BoundedChannelFullMode.DropOldest };
        options.FullMode.Should().Be(BoundedChannelFullMode.DropOldest);
    }

    // ── BufferedAuditStoreDecorator Tests ────────────────────────────────────────

    [Fact]
    public void Constructor_NullInnerStore_ThrowsArgumentNullException()
    {
        Action act = () => _ = new BufferedAuditStoreDecorator(null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("innerStore");
    }

    [Fact]
    public async Task Constructor_NullOptions_UsesDefaults()
    {
        var store = new TrackingStore();
        var decorator = new BufferedAuditStoreDecorator(store, options: null);
        var record = AuditRecordBuilder.Create().Build();

        await decorator.AppendAsync(record);
        await decorator.DisposeAsync();

        store.FlushedBatches.Should().HaveCount(1);
    }

    [Fact]
    public async Task Constructor_MultipleWorkers_StartsTasks()
    {
        var store = new TrackingStore();
        var options = new BufferedAuditStoreOptions { WorkerCount = 2, BatchSize = 10, FlushInterval = TimeSpan.FromMilliseconds(50) };
        var decorator = new BufferedAuditStoreDecorator(store, options);

        for (int i = 0; i < 20; i++)
        {
            await decorator.AppendAsync(AuditRecordBuilder.Create().WithAction($"A_{i}").Build());
        }

        await decorator.DisposeAsync();
        store.FlushedBatches.Count.Should().Be(20);
    }

    [Fact]
    public async Task AppendAsync_NullRecord_ThrowsArgumentNullException()
    {
        var decorator = new BufferedAuditStoreDecorator(new TrackingStore());
        Func<Task> act = async () => await decorator.AppendAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>().WithParameterName("record");
        await decorator.DisposeAsync();
    }

    [Fact]
    public async Task AppendBatchAsync_NullRecords_ThrowsArgumentNullException()
    {
        var decorator = new BufferedAuditStoreDecorator(new TrackingStore());
        Func<Task> act = async () => await decorator.AppendBatchAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>().WithParameterName("records");
        await decorator.DisposeAsync();
    }

    [Fact]
    public async Task QueryAsync_NullQuery_ThrowsArgumentNullException()
    {
        var decorator = new BufferedAuditStoreDecorator(new TrackingStore());
        Func<Task> act = async () => await decorator.QueryAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>().WithParameterName("query");
        await decorator.DisposeAsync();
    }

    [Fact]
    public async Task QueryAsync_DelegatesToInnerStore()
    {
        var mockStore = Substitute.For<IAuditStore>();
        var query = new AuditQuery { TenantId = "tenant-1" };
        var expectedResult = new AuditQueryResult(Array.Empty<AuditRecord>(), null, false);
#pragma warning disable CA2012
        mockStore.QueryAsync(query, Arg.Any<CancellationToken>()).Returns(ValueTask.FromResult(expectedResult));
#pragma warning restore CA2012

        var decorator = new BufferedAuditStoreDecorator(mockStore);
        var result = await decorator.QueryAsync(query);

        result.Should().BeSameAs(expectedResult);
        await decorator.DisposeAsync();
    }

    [Fact]
    public async Task Disposed_OperationsThrowObjectDisposedException()
    {
        var decorator = new BufferedAuditStoreDecorator(new TrackingStore());
        await decorator.DisposeAsync();

        Func<Task> actAppend = async () => await decorator.AppendAsync(AuditRecordBuilder.Create().Build());
        await actAppend.Should().ThrowAsync<ObjectDisposedException>();

        Func<Task> actBatch = async () => await decorator.AppendBatchAsync(new[] { AuditRecordBuilder.Create().Build() });
        await actBatch.Should().ThrowAsync<ObjectDisposedException>();

        // Double dispose is safe and idempotent
        await decorator.DisposeAsync();
    }

    [Fact]
    public void SynchronousDispose_DisposesInnerSyncDisposableStore()
    {
        var syncStore = new SyncDisposableStore();
        var decorator = new BufferedAuditStoreDecorator(syncStore);

        decorator.Dispose();
        syncStore.Disposed.Should().BeTrue();
    }

    [Fact]
    public async Task DisposeAsync_DisposesInnerAsyncDisposableStore()
    {
        var asyncStore = new TrackingStore();
        var decorator = new BufferedAuditStoreDecorator(asyncStore);

        await decorator.DisposeAsync();
        asyncStore.IsAsyncDisposed.Should().BeTrue();
    }

    [Fact]
    public async Task AppendBatchAsync_WritesAllRecords_AndDrainsCorrectly()
    {
        var store = new TrackingStore();
        var options = new BufferedAuditStoreOptions
        {
            Capacity = 100,
            BatchSize = 5,
            FlushInterval = TimeSpan.FromMilliseconds(50)
        };
        var decorator = new BufferedAuditStoreDecorator(store, options);

        var records = Enumerable.Range(0, 7)
            .Select(i => AuditRecordBuilder.Create().WithAction($"Action_{i}").WithTenant("t1").Build())
            .ToList();

        await decorator.AppendBatchAsync(records);
        await decorator.DisposeAsync();

        store.FlushedBatches.Count.Should().Be(7);
        store.BatchCalls.Count.Should().Be(2);
        store.BatchCalls[0].Count.Should().Be(5);
        store.BatchCalls[1].Count.Should().Be(2);
    }

    [Fact]
    public async Task MultiTenantBatch_SplitsByTenantAndFlushesIndependently()
    {
        var store = new TrackingStore();
        var options = new BufferedAuditStoreOptions
        {
            Capacity = 100,
            BatchSize = 10,
            FlushInterval = TimeSpan.FromMilliseconds(50)
        };
        var decorator = new BufferedAuditStoreDecorator(store, options);

        var rA1 = AuditRecordBuilder.Create().WithTenant("tenant-A").WithAction("A1").Build();
        var rB1 = AuditRecordBuilder.Create().WithTenant("tenant-B").WithAction("B1").Build();
        var rA2 = AuditRecordBuilder.Create().WithTenant("tenant-A").WithAction("A2").Build();
        var rC1 = AuditRecordBuilder.Create().WithTenant("tenant-C").WithAction("C1").Build();

        await decorator.AppendBatchAsync(new[] { rA1, rB1, rA2, rC1 });
        await decorator.DisposeAsync();

        store.FlushedBatches.Count.Should().Be(4);

        // Multi-tenant batch was split: should have separate calls for tenant-A, tenant-B, tenant-C
        store.BatchCalls.Count.Should().Be(3);
        store.BatchCalls.Should().Contain(b => b.All(r => r.Context.TenantId == "tenant-A") && b.Count == 2);
        store.BatchCalls.Should().Contain(b => b.All(r => r.Context.TenantId == "tenant-B") && b.Count == 1);
        store.BatchCalls.Should().Contain(b => b.All(r => r.Context.TenantId == "tenant-C") && b.Count == 1);
    }

    [Fact]
    public async Task RetryPipeline_TransientFailure_RetriesAndSuccessfullyFlushes()
    {
        var store = new TrackingStore { FailBatchCount = 1 }; // Fails once, succeeds on 2nd attempt
        var options = new BufferedAuditStoreOptions
        {
            Capacity = 100,
            BatchSize = 5,
            FlushInterval = TimeSpan.FromMilliseconds(20)
        };
        var decorator = new BufferedAuditStoreDecorator(store, options);

        for (int i = 0; i < 5; i++)
        {
            await decorator.AppendAsync(AuditRecordBuilder.Create().WithAction($"R_{i}").WithTenant("t1").Build());
        }

        await decorator.DisposeAsync();

        store.AppendBatchCallCount.Should().Be(2); // 1 failure + 1 retry success
        store.FlushedBatches.Count.Should().Be(5);
    }

    [Fact]
    public async Task PermanentFailure_WritesDeadLetterQueueFileAndLogsErrors()
    {
        var store = new TrackingStore { FailBatchCount = 99 }; // Fails all retries
        var options = new BufferedAuditStoreOptions
        {
            Capacity = 100,
            BatchSize = 3,
            FlushInterval = TimeSpan.FromMilliseconds(20)
        };

        var logger = new TestLogger<BufferedAuditStoreDecorator>();
        var decorator = new BufferedAuditStoreDecorator(store, options, logger);

        var record1 = AuditRecordBuilder.Create().WithAction("DLQ_1").WithTenant("t-dlq").Build();
        var record2 = AuditRecordBuilder.Create().WithAction("DLQ_2").WithTenant("t-dlq").Build();
        var record3 = AuditRecordBuilder.Create().WithAction("DLQ_3").WithTenant("t-dlq").Build();

        var tempDir = Path.GetTempPath();
        var initialFiles = Directory.GetFiles(tempDir, "audit-deadletter-*.json");

        try
        {
            await decorator.AppendBatchAsync(new[] { record1, record2, record3 });
            await decorator.DisposeAsync();

            // Verify logger was called for flush error and dead letter write
            logger.Logs.Should().Contain(l => l.Level == LogLevel.Error && l.Message.Contains("Failed to flush"));
            logger.Logs.Should().Contain(l => l.Level == LogLevel.Critical && l.Message.Contains("Dead Letter Queue"));

            // Verify a dead letter file was created in temp path and clean it up
            var currentFiles = Directory.GetFiles(tempDir, "audit-deadletter-*.json");
            var newFiles = currentFiles.Except(initialFiles).ToList();

            string? content = null;
            string? foundFile = null;
            foreach (var file in newFiles)
            {
                try
                {
                    var text = await File.ReadAllTextAsync(file);
                    if (text.Contains("DLQ_1"))
                    {
                        content = text;
                        foundFile = file;
                        break;
                    }
                }
                catch (FileNotFoundException) { }
            }

            content.Should().NotBeNull("a DLQ file containing the batch records must be created");
            content.Should().Contain("DLQ_2");
            content.Should().Contain("DLQ_3");

            if (foundFile != null)
            {
                try { File.Delete(foundFile); } catch { }
            }
        }
        catch
        {
            throw;
        }
    }

    [Fact]
    public async Task Constructor_CustomOptions_AppliesBatchSizeImmediately()
    {
        var store = new TrackingStore();
        var options = new BufferedAuditStoreOptions
        {
            BatchSize = 2,
            FlushInterval = TimeSpan.FromHours(1) // Long flush interval so only batch size triggers immediate flush
        };
        var decorator = new BufferedAuditStoreDecorator(store, options);

        await decorator.AppendAsync(AuditRecordBuilder.Create().WithAction("A1").Build());
        // Batch not full yet
        store.FlushedBatches.Should().BeEmpty();

        await decorator.AppendAsync(AuditRecordBuilder.Create().WithAction("A2").Build());
        // Batch size 2 reached, should flush
        for (int i = 0; i < 20 && store.FlushedBatches.Count < 2; i++)
        {
            await Task.Delay(25);
        }

        store.FlushedBatches.Count.Should().Be(2);
        await decorator.DisposeAsync();
    }

    [Fact]
    public async Task Dispose_SynchronouslyCompletesAndDrains()
    {
        var inner = new TrackingStore();
        var store = new BufferedAuditStoreDecorator(inner, new BufferedAuditStoreOptions { BatchSize = 10 });
        await store.AppendAsync(AuditRecordBuilder.Create().WithAction("SYNC").Build());
        store.Dispose();
        inner.FlushedBatches.Should().ContainSingle();
    }

    [Fact]
    public async Task PermanentFailure_WithNullLogger_DoesNotThrow()
    {
        var store = new TrackingStore { FailBatchCount = 99 };
        var decorator = new BufferedAuditStoreDecorator(store, new BufferedAuditStoreOptions { BatchSize = 1, FlushInterval = TimeSpan.FromMilliseconds(20) }, logger: null);
        await decorator.AppendAsync(AuditRecordBuilder.Create().WithAction("NO_LOG").Build());
        Func<Task> act = async () => await decorator.DisposeAsync();
        await act.Should().NotThrowAsync();
    }
}

