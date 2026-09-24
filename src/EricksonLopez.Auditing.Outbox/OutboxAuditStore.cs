// Copyright © Erickson Lopez. MIT License.
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EricksonLopez.Auditing;

namespace EricksonLopez.Auditing.Outbox;

/// <summary>Provides an audit store decorator that writes audit records to a transactional outbox rather than directly to the audit tables, ensuring atomic commits alongside business data.</summary>
public sealed class OutboxAuditStore : IAuditStore
{
    private readonly IOutboxMessageService _outbox;

    /// <summary>Initializes a new instance of the <see cref="OutboxAuditStore"/> class.</summary>
    /// <param name="outbox">The outbox message service.</param>
    /// <exception cref="ArgumentNullException"><paramref name="outbox"/> is <see langword="null"/></exception>
    public OutboxAuditStore(IOutboxMessageService outbox)
    {
        _outbox = outbox ?? throw new ArgumentNullException(nameof(outbox));
    }

    /// <inheritdoc />
    public async ValueTask AppendAsync(AuditRecord record, CancellationToken cancellationToken = default)
    {
        var payload = JsonSerializer.Serialize(record, OutboxJsonContext.Default.AuditRecord);
        await _outbox.AppendMessageAsync("AuditRecordCreated", payload, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask AppendBatchAsync(IReadOnlyList<AuditRecord> records, CancellationToken cancellationToken = default)
    {
        var payload = JsonSerializer.Serialize(records, OutboxJsonContext.Default.IReadOnlyListAuditRecord);
        await _outbox.AppendMessageAsync("AuditRecordBatchCreated", payload, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    /// <exception cref="NotSupportedException">Querying the audit store is not supported through the outbox decorator</exception>
    public ValueTask<AuditQueryResult> QueryAsync(AuditQuery query, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("Querying the audit store is not supported through the Outbox decorator. Resolve a direct IAuditStore implementation for queries.");
    }
}
