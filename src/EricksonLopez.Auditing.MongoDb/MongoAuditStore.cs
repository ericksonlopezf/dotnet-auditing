// Copyright © Erickson Lopez. MIT License.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MongoDB.Driver;

namespace EricksonLopez.Auditing.MongoDb;

/// <summary>Provides a MongoDB persistence store for immutable audit records.</summary>
public sealed class MongoAuditStore : IAuditStore
{
    private readonly IMongoCollection<MongoAuditRecordDocument> _collection;

    /// <summary>Initializes a new instance of the <see cref="MongoAuditStore"/> class with a MongoDB collection.</summary>
    /// <param name="collection">The MongoDB collection used to persist audit records.</param>
    /// <exception cref="ArgumentNullException"><paramref name="collection"/> is <see langword="null"/></exception>
    public MongoAuditStore(IMongoCollection<MongoAuditRecordDocument> collection)
    {
        _collection = collection ?? throw new ArgumentNullException(nameof(collection));
    }

    /// <summary>Initializes a new instance of the <see cref="MongoAuditStore"/> class with a database and configuration options.</summary>
    /// <param name="database">The MongoDB database hosting the audit collection.</param>
    /// <param name="options">The configuration options for the MongoDB audit store.</param>
    /// <exception cref="ArgumentNullException"><paramref name="database"/> or <paramref name="options"/> is <see langword="null"/></exception>
    public MongoAuditStore(IMongoDatabase database, MongoAuditStoreOptions options)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(options);

        _collection = database.GetCollection<MongoAuditRecordDocument>(options.CollectionName);
    }

    /// <inheritdoc/>
    public async ValueTask AppendAsync(AuditRecord record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);

        var doc = ToDocument(record);
        await _collection.InsertOneAsync(doc, cancellationToken: cancellationToken);
    }

    /// <inheritdoc/>
    public async ValueTask AppendBatchAsync(IReadOnlyList<AuditRecord> records, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(records);
        if (records.Count == 0)
            return;

        var docs = records.Select(ToDocument).ToList();
        await _collection.InsertManyAsync(docs, cancellationToken: cancellationToken);
    }

    /// <inheritdoc/>
    public async ValueTask<AuditQueryResult> QueryAsync(AuditQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentException.ThrowIfNullOrWhiteSpace(query.TenantId);

        var builder = Builders<MongoAuditRecordDocument>.Filter;
        var filter = builder.Eq(x => x.TenantId, query.TenantId);

        if (!string.IsNullOrWhiteSpace(query.ActorId))
            filter &= builder.Eq(x => x.ActorId, query.ActorId);

        if (!string.IsNullOrWhiteSpace(query.ActionCode))
            filter &= builder.Eq(x => x.ActionCode, query.ActionCode);

        if (!string.IsNullOrWhiteSpace(query.ResourceType))
            filter &= builder.Eq(x => x.ResourceType, query.ResourceType);

        if (!string.IsNullOrWhiteSpace(query.ResourceId))
            filter &= builder.Eq(x => x.ResourceId, query.ResourceId);

        if (query.Outcome.HasValue)
            filter &= builder.Eq(x => x.Outcome, (byte)query.Outcome.Value);

        if (query.From.HasValue)
            filter &= builder.Gte(x => x.OccurredAt, query.From.Value.UtcDateTime);

        if (query.To.HasValue)
            filter &= builder.Lte(x => x.OccurredAt, query.To.Value.UtcDateTime);

        if (!string.IsNullOrWhiteSpace(query.CorrelationId))
            filter &= builder.Eq(x => x.CorrelationId, query.CorrelationId);

        if (query.AfterRecordId.HasValue)
            filter &= builder.Gt(x => x.Id, query.AfterRecordId.Value);

        var pageSize = Math.Clamp(query.PageSize, 1, 1000);

        var docs = await _collection.Find(filter)
            .SortBy(x => x.Id)
            .Limit(pageSize + 1)
            .ToListAsync(cancellationToken);

        var hasMore = docs.Count > pageSize;
        var pageDocs = docs.Take(pageSize).ToList();
        var nextCursor = hasMore ? pageDocs[^1].Id : (Guid?)null;

        var records = pageDocs.Select(ToRecord).ToList();
        return new AuditQueryResult(records, nextCursor, hasMore);
    }

    private static MongoAuditRecordDocument ToDocument(AuditRecord record)
    {
        List<MongoAuditChangeDocument>? changes = null;
        if (record.Changes is { Count: > 0 })
        {
            changes = record.Changes.Select(c => new MongoAuditChangeDocument
            {
                Field = c.Field,
                OldValue = c.OldValue,
                NewValue = c.NewValue,
                IsRedacted = c.IsRedacted
            }).ToList();
        }

        return new MongoAuditRecordDocument
        {
            Id = record.Id,
            OccurredAt = record.OccurredAt.UtcDateTime,
            TenantId = record.Context.TenantId,
            Source = record.Context.Source,
            ActorType = (byte)record.Actor.Type,
            ActorId = record.Actor.Id,
            ActorName = record.Actor.DisplayName,
            ActionCode = record.Action.Code,
            ResourceType = record.Resource.Type,
            ResourceId = record.Resource.Id,
            AggregateType = record.Resource.AggregateType,
            AggregateId = record.Resource.AggregateId,
            Outcome = (byte)record.Outcome,
            ErrorCode = record.ErrorCode,
            CorrelationId = record.Context.CorrelationId,
            CausationId = record.Context.CausationId,
            RequestId = record.Context.RequestId,
            IpAddress = record.Context.IpAddress,
            UserAgent = record.Context.UserAgent,
            Changes = changes,
            IntegrityHash = record.IntegrityHash,
            PreviousHash = record.PreviousHash
        };
    }

    private static AuditRecord ToRecord(MongoAuditRecordDocument doc)
    {
        List<AuditChange>? changes = null;
        if (doc.Changes is { Count: > 0 })
        {
            changes = doc.Changes.Select(c => new AuditChange(c.Field, c.OldValue, c.NewValue, c.IsRedacted)).ToList();
        }

        return new AuditRecord
        {
            Id = doc.Id,
            OccurredAt = new DateTimeOffset(doc.OccurredAt, TimeSpan.Zero),
            Actor = new AuditActor((AuditActorType)doc.ActorType, doc.ActorId, doc.ActorName),
            Action = new AuditAction(doc.ActionCode),
            Resource = new AuditResource(doc.ResourceType, doc.ResourceId, doc.AggregateType, doc.AggregateId),
            Outcome = (AuditOutcome)doc.Outcome,
            Context = new AuditContext(doc.TenantId, doc.Source, doc.CorrelationId, doc.CausationId, doc.RequestId, doc.IpAddress, doc.UserAgent),
            Changes = changes,
            ErrorCode = doc.ErrorCode,
            IntegrityHash = doc.IntegrityHash,
            PreviousHash = doc.PreviousHash
        };
    }
}
