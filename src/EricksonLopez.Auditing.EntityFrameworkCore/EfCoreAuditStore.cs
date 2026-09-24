// Copyright © Erickson Lopez. MIT License.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace EricksonLopez.Auditing.EntityFrameworkCore;

/// <summary>Provides an Entity Framework Core persistence store for immutable audit records.</summary>
public sealed class EfCoreAuditStore : IAuditStore
{
    private readonly IDbContextFactory<AuditDbContext> _contextFactory;
    private readonly IAuditSensitivityPipeline? _sensitivityPipeline;

    /// <summary>Initializes a new instance of the <see cref="EfCoreAuditStore"/> class.</summary>
    /// <param name="contextFactory">The database context factory used to create <see cref="AuditDbContext"/> instances.</param>
    /// <param name="sensitivityPipeline">The optional sensitivity pipeline to sanitize records before persistence.</param>
    /// <exception cref="ArgumentNullException"><paramref name="contextFactory"/> is <see langword="null"/></exception>
    public EfCoreAuditStore(
        IDbContextFactory<AuditDbContext> contextFactory,
        IAuditSensitivityPipeline? sensitivityPipeline = null)
    {
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        _sensitivityPipeline = sensitivityPipeline;
    }

    /// <inheritdoc/>
    public async ValueTask AppendAsync(AuditRecord record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);

        if (_sensitivityPipeline is not null)
        {
            record = await _sensitivityPipeline.SanitizeAsync(record, cancellationToken).ConfigureAwait(false);
        }

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var entity = ToEntity(record);
        context.AuditRecords.Add(entity);
        await context.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async ValueTask AppendBatchAsync(IReadOnlyList<AuditRecord> records, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(records);
        if (records.Count == 0)
            return;

        if (_sensitivityPipeline is not null)
        {
            var sanitized = new List<AuditRecord>(records.Count);
            for (int i = 0; i < records.Count; i++)
            {
                sanitized.Add(await _sensitivityPipeline.SanitizeAsync(records[i], cancellationToken).ConfigureAwait(false));
            }
            records = sanitized;
        }

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var entities = records.Select(ToEntity).ToList();
        context.AuditRecords.AddRange(entities);
        await context.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async ValueTask<AuditQueryResult> QueryAsync(AuditQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentException.ThrowIfNullOrWhiteSpace(query.TenantId.Value, nameof(query.TenantId));

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var dbQuery = context.AuditRecords.AsNoTracking()
            .Where(e => e.TenantId == query.TenantId.Value);

        if (!string.IsNullOrWhiteSpace(query.ActorId))
            dbQuery = dbQuery.Where(e => e.ActorId == query.ActorId);

        if (!string.IsNullOrWhiteSpace(query.ActionCode))
            dbQuery = dbQuery.Where(e => e.ActionCode == query.ActionCode);

        if (!string.IsNullOrWhiteSpace(query.ResourceType))
            dbQuery = dbQuery.Where(e => e.ResourceType == query.ResourceType);

        if (!string.IsNullOrWhiteSpace(query.ResourceId))
            dbQuery = dbQuery.Where(e => e.ResourceId == query.ResourceId);

        if (query.Outcome.HasValue)
        {
            var outcomeVal = (byte)query.Outcome.Value;
            dbQuery = dbQuery.Where(e => e.Outcome == outcomeVal);
        }

        if (query.From.HasValue)
            dbQuery = dbQuery.Where(e => e.OccurredAt >= query.From.Value);

        if (query.To.HasValue)
            dbQuery = dbQuery.Where(e => e.OccurredAt <= query.To.Value);

        if (!string.IsNullOrWhiteSpace(query.CorrelationId))
            dbQuery = dbQuery.Where(e => e.CorrelationId == query.CorrelationId);

        if (AuditCursorToken.TryParse(query.ContinuationToken, out var cursorDate, out var cursorId))
        {
            dbQuery = dbQuery.Where(e => e.OccurredAt > cursorDate || (e.OccurredAt == cursorDate && e.Id > cursorId));
        }

        var pageSize = Math.Clamp(query.PageSize, 1, 1000);
        var entities = await dbQuery
            .OrderBy(e => e.OccurredAt)
            .ThenBy(e => e.Id)
            .Take(pageSize + 1)
            .ToListAsync(cancellationToken);

        var hasMore = entities.Count > pageSize;
        var pageEntities = entities.Take(pageSize).ToList();
        var nextCursor = hasMore ? AuditCursorToken.Create(pageEntities[^1].OccurredAt, pageEntities[^1].Id) : null;

        var records = pageEntities.Select(ToRecord).ToList();
        return new AuditQueryResult(records, nextCursor, hasMore);
    }

    private static AuditRecordEntity ToEntity(AuditRecord record)
    {
        string? changesJson = null;
        if (record.Changes is { Count: > 0 })
        {
            var dtos = record.Changes.Select(c => new AuditChangeDto(c.Field, c.OldValue, c.NewValue, c.IsRedacted)).ToList();
            changesJson = JsonSerializer.Serialize(dtos, EfCoreAuditJsonContext.Default.ListAuditChangeDto);
        }

        return new AuditRecordEntity
        {
            Id = record.Id,
            OccurredAt = record.OccurredAt,
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
            IdempotencyKey = record.Context.IdempotencyKey,
            ChangesJson = changesJson,
            IntegrityHash = record.IntegrityHash,
            PreviousHash = record.PreviousHash
        };
    }

    private static AuditRecord ToRecord(AuditRecordEntity entity)
    {
        List<AuditChange>? changes = null;
        if (!string.IsNullOrWhiteSpace(entity.ChangesJson))
        {
            var dtos = JsonSerializer.Deserialize(entity.ChangesJson, EfCoreAuditJsonContext.Default.ListAuditChangeDto);
            if (dtos is not null)
            {
                changes = dtos.Select(d => new AuditChange(d.Field, d.OldValue, d.NewValue, d.IsRedacted)).ToList();
            }
        }

        return new AuditRecord
        {
            Id = entity.Id,
            OccurredAt = entity.OccurredAt,
            Actor = new AuditActor((AuditActorType)entity.ActorType, entity.ActorId, entity.ActorName),
            Action = new AuditAction(entity.ActionCode),
            Resource = new AuditResource(entity.ResourceType, entity.ResourceId, entity.AggregateType, entity.AggregateId),
            Outcome = (AuditOutcome)entity.Outcome,
            Context = new AuditContext(entity.TenantId, entity.Source, entity.CorrelationId, entity.CausationId, entity.RequestId, entity.IpAddress, entity.UserAgent, entity.IdempotencyKey),
            Changes = changes,
            ErrorCode = entity.ErrorCode,
            IntegrityHash = entity.IntegrityHash,
            PreviousHash = entity.PreviousHash
        };
    }
}

