// Copyright © Erickson Lopez. MIT License.
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace EricksonLopez.Auditing.Testing;

/// <summary>Provides an in-memory, thread-safe persistence store for testing audit operations.</summary>
/// <remarks>This store is intended for test environments only and does not persist data across process restarts.</remarks>
public sealed class InMemoryAuditStore : IAuditStore
{
    private readonly ConcurrentQueue<AuditRecord> _records = new();

    /// <summary>Gets a snapshot of all audit records persisted in this store, in insertion order.</summary>
    public IReadOnlyList<AuditRecord> Records
    {
        get
        {
            var snapshot = _records.ToArray();
            return snapshot;
        }
    }

    /// <summary>Retrieves all persisted audit records belonging to the specified tenant.</summary>
    /// <param name="tenantId">The tenant identifier to filter by.</param>
    /// <returns>A read-only list of matching audit records in insertion order.</returns>
    public IReadOnlyList<AuditRecord> ForTenant(string tenantId) =>
        _records.Where(r => r.Context.TenantId == tenantId).ToList();

    /// <summary>Retrieves all persisted audit records associated with the specified actor.</summary>
    /// <param name="actorId">The actor identifier to filter by.</param>
    /// <returns>A read-only list of matching audit records in insertion order.</returns>
    public IReadOnlyList<AuditRecord> ForActor(string actorId) =>
        _records.Where(r => r.Actor.Id == actorId).ToList();

    /// <summary>Gets the total count of persisted audit records.</summary>
    public int Count => _records.Count;

    /// <summary>Removes all persisted audit records from the store.</summary>
    public void Clear()
    {
        while (_records.TryDequeue(out _)) { }
    }

    /// <inheritdoc/>
    public ValueTask AppendAsync(AuditRecord record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        cancellationToken.ThrowIfCancellationRequested();
        _records.Enqueue(record);
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask AppendBatchAsync(
        IReadOnlyList<AuditRecord> records,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(records);
        foreach (var record in records)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _records.Enqueue(record);
        }
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask<AuditQueryResult> QueryAsync(
        AuditQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        cancellationToken.ThrowIfCancellationRequested();

        IEnumerable<AuditRecord> filtered = _records
            .Where(r => r.Context.TenantId == query.TenantId)
            .Where(r => query.ActorId is null || r.Actor.Id == query.ActorId)
            .Where(r => query.ActionCode is null || r.Action.Code == query.ActionCode)
            .Where(r => query.ResourceType is null || r.Resource.Type == query.ResourceType)
            .Where(r => query.ResourceId is null || r.Resource.Id == query.ResourceId)
            .Where(r => query.Outcome is null || r.Outcome == query.Outcome)
            .Where(r => query.From is null || r.OccurredAt >= query.From)
            .Where(r => query.To is null || r.OccurredAt <= query.To)
            .Where(r => query.CorrelationId is null || r.Context.CorrelationId == query.CorrelationId)
            .OrderBy(r => r.OccurredAt)
            .ThenBy(r => r.Id);

        // Keyset pagination: skip records up to and including the cursor
        if (query.AfterRecordId.HasValue)
        {
            var afterId = query.AfterRecordId.Value;
            filtered = filtered.SkipWhile(r => r.Id != afterId).Skip(1);
        }

        var page = filtered.Take(query.PageSize + 1).ToList();
        var hasMore = page.Count > query.PageSize;

        if (hasMore) page.RemoveAt(page.Count - 1);

        var nextCursor = hasMore ? page[^1].Id : (Guid?)null;
        return ValueTask.FromResult(new AuditQueryResult(page, nextCursor, hasMore));
    }
}
