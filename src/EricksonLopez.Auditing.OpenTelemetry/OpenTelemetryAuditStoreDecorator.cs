// Copyright © Erickson Lopez. MIT License.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace EricksonLopez.Auditing.OpenTelemetry;

/// <summary>
/// Provides OpenTelemetry distributed tracing spans and metrics instrumentation for an underlying <see cref="IAuditStore"/>.
/// </summary>
public sealed class OpenTelemetryAuditStoreDecorator : IAuditStore
{
    private readonly IAuditStore _innerStore;

    /// <summary>Initializes a new instance of the <see cref="OpenTelemetryAuditStoreDecorator"/> class.</summary>
    /// <param name="innerStore">The underlying store to decorate with telemetry.</param>
    /// <exception cref="ArgumentNullException"><paramref name="innerStore"/> is <see langword="null"/></exception>
    public OpenTelemetryAuditStoreDecorator(IAuditStore innerStore)
    {
        _innerStore = innerStore ?? throw new ArgumentNullException(nameof(innerStore));
    }

    /// <inheritdoc/>
    public async ValueTask AppendAsync(AuditRecord record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);

        using var activity = AuditActivitySource.Source.StartActivity("Audit.Append");
        record.EnrichCurrentActivity();
        var sw = Stopwatch.StartNew();

        try
        {
            await _innerStore.AppendAsync(record, cancellationToken).ConfigureAwait(false);
            sw.Stop();
            AuditMetrics.AppendDuration.Record(sw.Elapsed.TotalMilliseconds);
            AuditMetrics.RecordsAppended.Add(1,
                new KeyValuePair<string, object?>(AuditActivitySource.Tags.TenantId, record.Context.TenantId.Value),
                new KeyValuePair<string, object?>("audit.outcome", record.Outcome.ToString()));
        }
        catch (Exception ex)
        {
            sw.Stop();
            AuditMetrics.AppendDuration.Record(sw.Elapsed.TotalMilliseconds);
            AuditMetrics.RecordsFailed.Add(1,
                new KeyValuePair<string, object?>(AuditActivitySource.Tags.TenantId, record.Context.TenantId.Value),
                new KeyValuePair<string, object?>("audit.outcome", record.Outcome.ToString()));
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }

    /// <inheritdoc/>
    public async ValueTask AppendBatchAsync(IReadOnlyList<AuditRecord> records, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(records);

        if (records.Count == 0)
            return;

        using var activity = AuditActivitySource.Source.StartActivity("Audit.AppendBatch");
        if (activity is not null)
        {
            activity.SetTag(AuditActivitySource.Tags.TenantId, records[0].Context.TenantId.Value);
            activity.SetTag("audit.batch_size", records.Count);
        }

        var sw = Stopwatch.StartNew();
        try
        {
            await _innerStore.AppendBatchAsync(records, cancellationToken).ConfigureAwait(false);
            sw.Stop();
            AuditMetrics.AppendDuration.Record(sw.Elapsed.TotalMilliseconds);

            var tenantId = records[0].Context.TenantId.Value;
            AuditMetrics.RecordsAppended.Add(records.Count,
                new KeyValuePair<string, object?>(AuditActivitySource.Tags.TenantId, tenantId));
        }
        catch (Exception ex)
        {
            sw.Stop();
            AuditMetrics.AppendDuration.Record(sw.Elapsed.TotalMilliseconds);
            AuditMetrics.RecordsFailed.Add(records.Count,
                new KeyValuePair<string, object?>(AuditActivitySource.Tags.TenantId, records[0].Context.TenantId.Value));
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }

    /// <inheritdoc/>
    public async ValueTask<AuditQueryResult> QueryAsync(AuditQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        using var activity = AuditActivitySource.Source.StartActivity("Audit.Query");
        if (activity is not null)
        {
            activity.SetTag(AuditActivitySource.Tags.TenantId, query.TenantId.Value);
        }

        var sw = Stopwatch.StartNew();
        try
        {
            var result = await _innerStore.QueryAsync(query, cancellationToken).ConfigureAwait(false);
            sw.Stop();
            AuditMetrics.QueryDuration.Record(sw.Elapsed.TotalMilliseconds);

            AuditMetrics.QueriesExecuted.Add(1,
                new KeyValuePair<string, object?>(AuditActivitySource.Tags.TenantId, query.TenantId.Value));

            return result;
        }
        catch (Exception ex)
        {
            sw.Stop();
            AuditMetrics.QueryDuration.Record(sw.Elapsed.TotalMilliseconds);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }
}

