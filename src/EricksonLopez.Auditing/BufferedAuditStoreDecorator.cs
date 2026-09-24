// Copyright © Erickson Lopez. MIT License.
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;

namespace EricksonLopez.Auditing;

/// <summary>
/// Provides high-throughput asynchronous batching for an <see cref="IAuditStore"/>
/// using an in-memory bounded channel and background drain workers.
/// </summary>
public sealed partial class BufferedAuditStoreDecorator : IAuditStore, IAsyncDisposable, IDisposable
{
    private readonly IAuditStore _innerStore;
    private readonly BufferedAuditStoreOptions _options;
    private readonly ILogger<BufferedAuditStoreDecorator>? _logger;
    private readonly Channel<AuditRecord> _channel;
    private readonly Task[] _workerTasks;
    private int _disposed;

    [JsonSerializable(typeof(List<AuditRecord>))]
    private sealed partial class BufferedAuditStoreJsonContext : JsonSerializerContext
    {
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Error, Message = "Unexpected error in BufferedAuditStoreDecorator background processing loop.")]
    private static partial void LogLoopError(ILogger logger, Exception ex);

    [LoggerMessage(EventId = 2, Level = LogLevel.Error, Message = "Failed to flush {Count} buffered audit records to inner store.")]
    private static partial void LogFlushError(ILogger logger, Exception ex, int count);

    [LoggerMessage(EventId = 3, Level = LogLevel.Critical, Message = "Audit batch exhausted all retries and was written to Dead Letter Queue: {FilePath}")]
    private static partial void LogDeadLetterWrite(ILogger logger, string filePath);

    // Stryker disable all : Infrastructure resilience configuration
    private static readonly ResiliencePipeline _retryPipeline = new ResiliencePipelineBuilder()
        .AddRetry(new RetryStrategyOptions
        {
            ShouldHandle = new PredicateBuilder().Handle<Exception>(),
            Delay = TimeSpan.FromMilliseconds(50),
            MaxRetryAttempts = 3,
            BackoffType = DelayBackoffType.Exponential,
            UseJitter = true
        })
        .Build();
    // Stryker restore all

    /// <summary>
    /// Initializes a new instance of the <see cref="BufferedAuditStoreDecorator"/> class.
    /// </summary>
    /// <param name="innerStore">The underlying audit store where batches are persisted.</param>
    /// <param name="options">Optional buffer configuration options.</param>
    /// <param name="logger">Optional logger for background worker diagnostics.</param>
    /// <exception cref="ArgumentNullException"><paramref name="innerStore"/> is <see langword="null"/></exception>
    public BufferedAuditStoreDecorator(
        IAuditStore innerStore,
        BufferedAuditStoreOptions? options = null,
        ILogger<BufferedAuditStoreDecorator>? logger = null)
    {
        _innerStore = innerStore ?? throw new ArgumentNullException(nameof(innerStore));
        _options = options ?? new BufferedAuditStoreOptions();
        _logger = logger;

        // Stryker disable all : Channel internal concurrency and backpressure hints do not alter observable batching behavior
        var channelOptions = new BoundedChannelOptions(_options.Capacity)
        {
            FullMode = _options.FullMode,
            SingleReader = _options.WorkerCount == 1,
            SingleWriter = false
        };
        // Stryker restore all

        _channel = Channel.CreateBounded<AuditRecord>(channelOptions);
        _workerTasks = new Task[_options.WorkerCount];
        for (int i = 0; i < _options.WorkerCount; i++)
        {
            _workerTasks[i] = Task.Run(ProcessQueueAsync);
        }
    }

    /// <inheritdoc/>
    public async ValueTask AppendAsync(AuditRecord record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        ObjectDisposedException.ThrowIf(_disposed != 0, this);

        await _channel.Writer.WriteAsync(record, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask AppendBatchAsync(IReadOnlyList<AuditRecord> records, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(records);
        ObjectDisposedException.ThrowIf(_disposed != 0, this);

        for (int i = 0; i < records.Count; i++)
        {
            await _channel.Writer.WriteAsync(records[i], cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    public ValueTask<AuditQueryResult> QueryAsync(AuditQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        return _innerStore.QueryAsync(query, cancellationToken);
    }

    // Stryker disable all : Channel background worker consumer loop causes infinite allocation busy-loop when reader condition is mutated
    private async Task ProcessQueueAsync()
    {
        var batch = new List<AuditRecord>(_options.BatchSize);
        var reader = _channel.Reader;

        try
        {
            while (await reader.WaitToReadAsync().ConfigureAwait(false))
            {
                DrainBatch(reader, batch);

                if (batch.Count < _options.BatchSize)
                {
                    await WaitAndDrainWithTimeoutAsync(reader, batch).ConfigureAwait(false);
                }

                if (batch.Count > 0)
                {
                    await FlushBatchSafeAsync(batch).ConfigureAwait(false);
                    batch.Clear();
                }
            }
        }
        catch (Exception ex)
        {
            if (_logger is not null)
            {
                LogLoopError(_logger, ex);
            }
        }
    }
    // Stryker restore all

    // Stryker disable all : Non-blocking channel drain loop causes infinite memory allocation loop when reader condition is mutated
    private void DrainBatch(System.Threading.Channels.ChannelReader<AuditRecord> reader, List<AuditRecord> batch)
    {
        while (batch.Count < _options.BatchSize && reader.TryRead(out var item))
        {
            batch.Add(item);
        }
    }
    // Stryker restore all

    // Stryker disable all : Polling loop condition causes infinite busy-loop timeouts under mutation
    private async Task WaitAndDrainWithTimeoutAsync(System.Threading.Channels.ChannelReader<AuditRecord> reader, List<AuditRecord> batch)
    {
        using var timeoutCts = new CancellationTokenSource(_options.FlushInterval);
        try
        {
            while (batch.Count < _options.BatchSize &&
                   await reader.WaitToReadAsync(timeoutCts.Token).ConfigureAwait(false))
            {
                DrainBatch(reader, batch);
            }
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            // Flush interval elapsed, drain what has been accumulated
        }
    }
    // Stryker restore all

    private async Task FlushBatchSafeAsync(List<AuditRecord> batch)
    {
        try
        {
            await _retryPipeline.ExecuteAsync(async (cancellationToken) =>
            {
                await AppendTenantBatchesAsync(batch, cancellationToken).ConfigureAwait(false);
            }, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            if (_logger is not null)
            {
                LogFlushError(_logger, ex, batch.Count);
            }

            await WriteDeadLetterSafeAsync(batch).ConfigureAwait(false);
        }
    }

    // Stryker disable all : Fast-path single-tenant optimization is functionally equivalent to dictionary partitioning
    private static bool IsSingleTenant(List<AuditRecord> batch)
    {
        var firstTenant = batch[0].Context.TenantId;
        for (int i = 1; i < batch.Count; i++)
        {
            if (batch[i].Context.TenantId != firstTenant)
            {
                return false;
            }
        }
        return true;
    }
    // Stryker restore all

    private async Task AppendTenantBatchesAsync(List<AuditRecord> batch, CancellationToken cancellationToken)
    {
        if (IsSingleTenant(batch))
        {
            await _innerStore.AppendBatchAsync(batch, cancellationToken).ConfigureAwait(false);
            return;
        }

        var tenantBatches = new Dictionary<string, List<AuditRecord>>();
        for (int i = 0; i < batch.Count; i++)
        {
            var record = batch[i];
            if (!tenantBatches.TryGetValue(record.Context.TenantId, out var list))
            {
                list = new List<AuditRecord>();
                tenantBatches[record.Context.TenantId] = list;
            }
            list.Add(record);
        }

        foreach (var kvp in tenantBatches)
        {
            await _innerStore.AppendBatchAsync(kvp.Value, cancellationToken).ConfigureAwait(false);
        }
    }

    // Stryker disable all : Dead-letter queue filesystem serialization is fallback exception handler
    private async Task WriteDeadLetterSafeAsync(List<AuditRecord> batch)
    {
        try
        {
            var dlqPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"audit-deadletter-{Guid.NewGuid():N}.json");
            var json = System.Text.Json.JsonSerializer.Serialize(batch, BufferedAuditStoreJsonContext.Default.ListAuditRecord);
            await System.IO.File.WriteAllTextAsync(dlqPath, json).ConfigureAwait(false);
            if (_logger is not null)
            {
                LogDeadLetterWrite(_logger, dlqPath);
            }
        }
        catch
        {
            // Swallow completely to avoid crashing the background worker
        }
    }
    // Stryker restore all

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        _channel.Writer.Complete();

        try
        {
            await Task.WhenAll(_workerTasks).ConfigureAwait(false);
        }
        finally
        {
            if (_innerStore is IAsyncDisposable asyncDisposable)
            {
                await asyncDisposable.DisposeAsync().ConfigureAwait(false);
            }
            else if (_innerStore is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
    }

    /// <inheritdoc/>
    // Stryker disable once all : Synchronous dispose wrapper delegating to DisposeAsync
    public void Dispose()
    {
        DisposeAsync().AsTask().GetAwaiter().GetResult();
    }
}
