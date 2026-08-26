// Copyright © Erickson Lopez. MIT License.
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Dapper;

namespace EricksonLopez.Auditing.Dapper;

/// <summary>Provides a database-agnostic audit store implementation using Dapper and standard ANSI SQL.</summary>
public sealed class DapperAuditStore : IAuditStore
{
    private readonly DapperAuditStoreOptions _options;
    private readonly string _table;

    /// <summary>Initializes a new instance of the <see cref="DapperAuditStore"/> class.</summary>
    /// <param name="options">The configuration options for the Dapper audit store.</param>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <see langword="null"/></exception>
    public DapperAuditStore(DapperAuditStoreOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _table = string.IsNullOrWhiteSpace(options.Table) ? "audit_records" : options.Table;
    }

    /// <inheritdoc/>
    public async ValueTask AppendAsync(
        AuditRecord record,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);

        using var connection = _options.ConnectionFactory();
        EnsureOpenConnection(connection);

        var sql = $@"
            INSERT INTO {_table} (
                id, occurred_at, tenant_id, source,
                actor_type, actor_id, actor_name, action_code,
                resource_type, resource_id, aggregate_type, aggregate_id,
                outcome, error_code,
                correlation_id, causation_id, request_id, ip_address, user_agent,
                changes_json, integrity_hash, previous_hash
            ) VALUES (
                @Id, @OccurredAt, @TenantId, @Source,
                @ActorType, @ActorId, @ActorName, @ActionCode,
                @ResourceType, @ResourceId, @AggregateType, @AggregateId,
                @Outcome, @ErrorCode,
                @CorrelationId, @CausationId, @RequestId, @IpAddress, @UserAgent,
                @Changes, @IntegrityHash, @PreviousHash
            );";

        var parameters = ToParameters(record);
        await connection.ExecuteAsync(new CommandDefinition(
            sql,
            parameters,
            cancellationToken: cancellationToken));
    }

    /// <inheritdoc/>
    public async ValueTask AppendBatchAsync(
        IReadOnlyList<AuditRecord> records,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(records);

        if (records.Count == 0)
        {
            return;
        }

        using var connection = _options.ConnectionFactory();
        EnsureOpenConnection(connection);

        using var tx = connection.BeginTransaction();

        var sql = $@"
            INSERT INTO {_table} (
                id, occurred_at, tenant_id, source,
                actor_type, actor_id, actor_name, action_code,
                resource_type, resource_id, aggregate_type, aggregate_id,
                outcome, error_code,
                correlation_id, causation_id, request_id, ip_address, user_agent,
                changes_json, integrity_hash, previous_hash
            ) VALUES (
                @Id, @OccurredAt, @TenantId, @Source,
                @ActorType, @ActorId, @ActorName, @ActionCode,
                @ResourceType, @ResourceId, @AggregateType, @AggregateId,
                @Outcome, @ErrorCode,
                @CorrelationId, @CausationId, @RequestId, @IpAddress, @UserAgent,
                @Changes, @IntegrityHash, @PreviousHash
            );";

        foreach (var record in records)
        {
            var parameters = ToParameters(record);
            await connection.ExecuteAsync(new CommandDefinition(
                sql,
                parameters,
                transaction: tx,
                cancellationToken: cancellationToken));
        }

        tx.Commit();
    }

    /// <inheritdoc/>
    public async ValueTask<AuditQueryResult> QueryAsync(
        AuditQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentException.ThrowIfNullOrWhiteSpace(query.TenantId);

        using var connection = _options.ConnectionFactory();
        EnsureOpenConnection(connection);

        var conditions = new List<string> { "tenant_id = @TenantId" };
        var dynamicParams = new DynamicParameters();
        dynamicParams.Add("TenantId", query.TenantId);

        if (!string.IsNullOrWhiteSpace(query.ActorId))
        {
            conditions.Add("actor_id = @ActorId");
            dynamicParams.Add("ActorId", query.ActorId);
        }

        if (!string.IsNullOrWhiteSpace(query.ActionCode))
        {
            conditions.Add("action_code = @ActionCode");
            dynamicParams.Add("ActionCode", query.ActionCode);
        }

        if (!string.IsNullOrWhiteSpace(query.ResourceType))
        {
            conditions.Add("resource_type = @ResourceType");
            dynamicParams.Add("ResourceType", query.ResourceType);
        }

        if (!string.IsNullOrWhiteSpace(query.ResourceId))
        {
            conditions.Add("resource_id = @ResourceId");
            dynamicParams.Add("ResourceId", query.ResourceId);
        }

        if (query.Outcome.HasValue)
        {
            conditions.Add("outcome = @Outcome");
            dynamicParams.Add("Outcome", (byte)query.Outcome.Value);
        }

        if (query.From.HasValue)
        {
            conditions.Add("occurred_at >= @From");
            dynamicParams.Add("From", query.From.Value);
        }

        if (query.To.HasValue)
        {
            conditions.Add("occurred_at <= @To");
            dynamicParams.Add("To", query.To.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.CorrelationId))
        {
            conditions.Add("correlation_id = @CorrelationId");
            dynamicParams.Add("CorrelationId", query.CorrelationId);
        }

        if (query.AfterRecordId.HasValue)
        {
            conditions.Add("id > @AfterRecordId");
            dynamicParams.Add("AfterRecordId", query.AfterRecordId.Value);
        }

        var whereClause = string.Join(" AND ", conditions);
        var pageSize = Math.Clamp(query.PageSize, 1, 1000);
        dynamicParams.Add("Limit", pageSize + 1);

        var sql = $@"
            SELECT
                id, occurred_at AS OccurredAt, tenant_id AS TenantId, source,
                actor_type AS ActorType, actor_id AS ActorId, actor_name AS ActorName,
                action_code AS ActionCode,
                resource_type AS ResourceType, resource_id AS ResourceId,
                aggregate_type AS AggregateType, aggregate_id AS AggregateId,
                outcome, error_code AS ErrorCode,
                correlation_id AS CorrelationId, causation_id AS CausationId,
                request_id AS RequestId, ip_address AS IpAddress, user_agent AS UserAgent,
                changes_json AS ChangesJson, integrity_hash AS IntegrityHash, previous_hash AS PreviousHash
            FROM {_table}
            WHERE {whereClause}
            ORDER BY id ASC
            LIMIT @Limit;";

        var rows = (await connection.QueryAsync<AuditRecordRow>(
            new CommandDefinition(
                sql,
                dynamicParams,
                cancellationToken: cancellationToken))).ToList();

        var hasMore = rows.Count > pageSize;
        var pageRows = rows.Take(pageSize).ToList();
        var nextCursor = hasMore
            ? pageRows[^1].Id
            : (Guid?)null;

        var records = pageRows.Select(MapRow).ToList();
        return new AuditQueryResult(records, nextCursor, hasMore);
    }

    /// <summary>Retrieves a single audit record by its unique identifier and tenant scope.</summary>
    /// <param name="id">The unique identifier of the audit record to retrieve.</param>
    /// <param name="tenantId">The tenant identifier scoping the lookup.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the asynchronous operation.</param>
    /// <returns>A value task representing the asynchronous operation. The task result contains the matching <see cref="AuditRecord"/>, or <see langword="null"/> if not found.</returns>
    /// <exception cref="ArgumentException"><paramref name="tenantId"/> is <see langword="null"/> or empty</exception>
    public async ValueTask<AuditRecord?> GetByIdAsync(
        Guid id,
        string tenantId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);

        using var connection = _options.ConnectionFactory();
        EnsureOpenConnection(connection);

        var sql = $@"
            SELECT
                id, occurred_at AS OccurredAt, tenant_id AS TenantId, source,
                actor_type AS ActorType, actor_id AS ActorId, actor_name AS ActorName,
                action_code AS ActionCode,
                resource_type AS ResourceType, resource_id AS ResourceId,
                aggregate_type AS AggregateType, aggregate_id AS AggregateId,
                outcome, error_code AS ErrorCode,
                correlation_id AS CorrelationId, causation_id AS CausationId,
                request_id AS RequestId, ip_address AS IpAddress, user_agent AS UserAgent,
                changes_json AS ChangesJson, integrity_hash AS IntegrityHash, previous_hash AS PreviousHash
            FROM {_table}
            WHERE id = @Id AND tenant_id = @TenantId
            LIMIT 1;";

        var row = await connection.QuerySingleOrDefaultAsync<AuditRecordRow>(
            new CommandDefinition(
                sql,
                new { Id = id, TenantId = tenantId },
                cancellationToken: cancellationToken));

        return row is null ? null : MapRow(row);
    }

    private static void EnsureOpenConnection(IDbConnection connection)
    {
        if (connection.State != ConnectionState.Open)
        {
            connection.Open();
        }
    }

    private static DynamicParameters ToParameters(AuditRecord record)
    {
        var p = new DynamicParameters();
        p.Add("Id", record.Id);
        p.Add("OccurredAt", record.OccurredAt);
        p.Add("TenantId", record.Context.TenantId);
        p.Add("Source", record.Context.Source);
        p.Add("ActorType", (byte)record.Actor.Type);
        p.Add("ActorId", record.Actor.Id);
        p.Add("ActorName", record.Actor.DisplayName);
        p.Add("ActionCode", record.Action.Code);
        p.Add("ResourceType", record.Resource.Type);
        p.Add("ResourceId", record.Resource.Id);
        p.Add("AggregateType", record.Resource.AggregateType);
        p.Add("AggregateId", record.Resource.AggregateId);
        p.Add("Outcome", (byte)record.Outcome);
        p.Add("ErrorCode", record.ErrorCode);
        p.Add("CorrelationId", record.Context.CorrelationId);
        p.Add("CausationId", record.Context.CausationId);
        p.Add("RequestId", record.Context.RequestId);
        p.Add("IpAddress", record.Context.IpAddress);
        p.Add("UserAgent", record.Context.UserAgent);
        p.Add("Changes", SerializeChanges(record.Changes));
        p.Add("IntegrityHash", record.IntegrityHash);
        p.Add("PreviousHash", record.PreviousHash);
        return p;
    }

    private static string? SerializeChanges(IReadOnlyList<AuditChange>? changes)
    {
        if (changes is null || changes.Count == 0) return null;

        var dtos = new List<AuditChangeDto>(changes.Count);
        foreach (var c in changes)
        {
            dtos.Add(new AuditChangeDto(c.Field, c.OldValue, c.NewValue, c.IsRedacted));
        }

        return JsonSerializer.Serialize(dtos, DapperAuditJsonContext.Default.ListAuditChangeDto);
    }

    private static AuditRecord MapRow(AuditRecordRow row)
    {
        var changes = DeserializeChanges(row.ChangesJson);

        return new AuditRecord
        {
            Id = row.Id,
            OccurredAt = row.OccurredAt,
            Actor = new AuditActor((AuditActorType)row.ActorType, row.ActorId!, row.ActorName),
            Action = new AuditAction(row.ActionCode!),
            Resource = new AuditResource(row.ResourceType!, row.ResourceId!, row.AggregateType, row.AggregateId),
            Outcome = (AuditOutcome)row.Outcome,
            ErrorCode = row.ErrorCode,
            Context = new AuditContext(
                TenantId: row.TenantId!,
                Source: row.Source!,
                CorrelationId: row.CorrelationId,
                CausationId: row.CausationId,
                RequestId: row.RequestId,
                IpAddress: row.IpAddress,
                UserAgent: row.UserAgent),
            Changes = changes,
            IntegrityHash = row.IntegrityHash,
            PreviousHash = row.PreviousHash
        };
    }

    private static AuditChange[]? DeserializeChanges(string? json)
    {
        if (string.IsNullOrEmpty(json)) return null;

        var dtos = JsonSerializer.Deserialize(json, DapperAuditJsonContext.Default.ListAuditChangeDto);
        if (dtos is null || dtos.Count == 0) return null;

        var result = new AuditChange[dtos.Count];
        for (int i = 0; i < dtos.Count; i++)
        {
            var d = dtos[i];
            result[i] = new AuditChange(d.Field, d.OldValue, d.NewValue, d.IsRedacted);
        }

        return result;
    }

    private sealed class AuditRecordRow
    {
        public Guid Id { get; set; }
        public DateTimeOffset OccurredAt { get; set; }
        public string? TenantId { get; set; }
        public string? Source { get; set; }
        public byte ActorType { get; set; }
        public string? ActorId { get; set; }
        public string? ActorName { get; set; }
        public string? ActionCode { get; set; }
        public string? ResourceType { get; set; }
        public string? ResourceId { get; set; }
        public string? AggregateType { get; set; }
        public string? AggregateId { get; set; }
        public byte Outcome { get; set; }
        public string? ErrorCode { get; set; }
        public string? CorrelationId { get; set; }
        public string? CausationId { get; set; }
        public string? RequestId { get; set; }
        public string? IpAddress { get; set; }
        public string? UserAgent { get; set; }
        public string? ChangesJson { get; set; }
        public string? IntegrityHash { get; set; }
        public string? PreviousHash { get; set; }
    }
}
