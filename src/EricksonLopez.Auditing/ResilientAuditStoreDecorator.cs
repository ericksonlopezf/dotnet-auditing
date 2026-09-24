// Copyright © Erickson Lopez. MIT License.
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace EricksonLopez.Auditing;

/// <summary>
/// Provides failure resilience policies (<see cref="AuditFailureBehavior"/>) and critical action protection
/// for an underlying <see cref="IAuditStore"/>.
/// </summary>
public sealed partial class ResilientAuditStoreDecorator : IAuditStore
{
    private readonly IAuditStore _innerStore;
    private readonly AuditConfiguration _configuration;
    private readonly ILogger<ResilientAuditStoreDecorator>? _logger;

    [LoggerMessage(EventId = 1, Level = LogLevel.Warning, Message = "Failed to persist audit record {RecordId} for tenant {TenantId}. Action: {ActionCode}. Failure behavior policy: {Policy}")]
    private static partial void LogAppendWarning(ILogger logger, Exception ex, Guid recordId, string tenantId, string actionCode, AuditFailureBehavior policy);

    [LoggerMessage(EventId = 2, Level = LogLevel.Warning, Message = "Failed to persist batch of {Count} audit records for tenant {TenantId}. Failure behavior policy: {Policy}")]
    private static partial void LogAppendBatchWarning(ILogger logger, Exception ex, int count, string tenantId, AuditFailureBehavior policy);

    /// <summary>Initializes a new instance of the <see cref="ResilientAuditStoreDecorator"/> class.</summary>
    /// <param name="innerStore">The underlying audit store to decorate.</param>
    /// <param name="configuration">The audit configuration defining failure behaviors.</param>
    /// <param name="logger">Optional logger for diagnostic warnings when operating in FailOpen mode.</param>
    /// <exception cref="ArgumentNullException"><paramref name="innerStore"/> or <paramref name="configuration"/> is <see langword="null"/></exception>
    public ResilientAuditStoreDecorator(
        IAuditStore innerStore,
        AuditConfiguration configuration,
        ILogger<ResilientAuditStoreDecorator>? logger = null)
    {
        _innerStore = innerStore ?? throw new ArgumentNullException(nameof(innerStore));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger;
    }

    [System.Text.Json.Serialization.JsonSerializable(typeof(AuditRecord))]
    [System.Text.Json.Serialization.JsonSerializable(typeof(IReadOnlyList<AuditRecord>))]
    private sealed partial class ResilientAuditStoreJsonContext : System.Text.Json.Serialization.JsonSerializerContext
    {
    }

    /// <inheritdoc/>
    public async ValueTask AppendAsync(AuditRecord record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);

        try
        {
            await _innerStore.AppendAsync(record, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            if (_logger is not null)
            {
                LogAppendWarning(_logger, ex, record.Id, record.Context.TenantId, record.Action.Code, _configuration.DefaultFailureBehavior);
            }

            if (ShouldPropagate(record))
            {
                throw;
            }

            if (_configuration.DefaultFailureBehavior == AuditFailureBehavior.Deferred)
            {
                await EnqueueDeferredAsync(record).ConfigureAwait(false);
            }
        }
    }

    /// <inheritdoc/>
    public async ValueTask AppendBatchAsync(IReadOnlyList<AuditRecord> records, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(records);

        if (records.Count == 0)
            return;

        try
        {
            await _innerStore.AppendBatchAsync(records, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            if (_logger is not null)
            {
                LogAppendBatchWarning(_logger, ex, records.Count, records[0].Context.TenantId, _configuration.DefaultFailureBehavior);
            }

            if (ShouldPropagateBatch(records))
            {
                throw;
            }

            if (_configuration.DefaultFailureBehavior == AuditFailureBehavior.Deferred)
            {
                await EnqueueDeferredBatchAsync(records).ConfigureAwait(false);
            }
        }
    }

    /// <inheritdoc/>
    public ValueTask<AuditQueryResult> QueryAsync(AuditQuery query, CancellationToken cancellationToken = default)
    {
        return _innerStore.QueryAsync(query, cancellationToken);
    }

    private static async Task EnqueueDeferredAsync(AuditRecord record)
    {
        try
        {
            var dlqPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"audit-deferred-{record.Id:N}.json");
            var json = System.Text.Json.JsonSerializer.Serialize(record, ResilientAuditStoreJsonContext.Default.AuditRecord);
            await System.IO.File.WriteAllTextAsync(dlqPath, json).ConfigureAwait(false);
        }
        catch
        {
            // Suppress fallback errors to preserve resilience contract
        }
    }

    // Stryker disable all : Dead-letter fallback serialization to temp path
    private static async Task EnqueueDeferredBatchAsync(IReadOnlyList<AuditRecord> records)
    {
        try
        {
            var dlqPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"audit-deferred-batch-{Guid.NewGuid():N}.json");
            var json = System.Text.Json.JsonSerializer.Serialize(records, ResilientAuditStoreJsonContext.Default.IReadOnlyListAuditRecord);
            await System.IO.File.WriteAllTextAsync(dlqPath, json).ConfigureAwait(false);
        }
        catch
        {
            // Suppress fallback errors to preserve resilience contract
        }
    }
    // Stryker restore all

    private bool ShouldPropagate(AuditRecord record)
    {
        if (_configuration.CriticalActionCodes.Contains(record.Action.Code))
        {
            return true;
        }

        return _configuration.DefaultFailureBehavior == AuditFailureBehavior.FailClosed;
    }

    private bool ShouldPropagateBatch(IReadOnlyList<AuditRecord> records)
    {
        for (int i = 0; i < records.Count; i++)
        {
            if (_configuration.CriticalActionCodes.Contains(records[i].Action.Code))
            {
                return true;
            }
        }

        return _configuration.DefaultFailureBehavior == AuditFailureBehavior.FailClosed;
    }
}
