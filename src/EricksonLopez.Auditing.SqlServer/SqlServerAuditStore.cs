// Copyright © Erickson Lopez. MIT License.
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Dapper;

namespace EricksonLopez.Auditing.SqlServer;

/// <summary>Provides a Microsoft SQL Server persistence store for immutable audit records using Dapper.</summary>
public sealed class SqlServerAuditStore : IAuditStore
{
    private readonly SqlServerAuditStoreOptions _options;
    private readonly string _qualifiedTable;

    /// <summary>Initializes a new instance of the <see cref="SqlServerAuditStore"/> class.</summary>
    /// <param name="options">The SQL Server audit store configuration options.</param>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <see langword="null"/></exception>
    public SqlServerAuditStore(SqlServerAuditStoreOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _qualifiedTable = $"[{options.Schema}].[{options.Table}]";
    }

    /// <inheritdoc/>
    public async ValueTask AppendAsync(AuditRecord record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);

        using var connection = _options.ConnectionFactory();
        await SetRlsContextAsync(connection, record.Context.TenantId);

        await connection.ExecuteAsync(BuildInsertSql(), ToParameters(record));
    }

    /// <inheritdoc/>
    public async ValueTask AppendBatchAsync(
        IReadOnlyList<AuditRecord> records,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(records);
        if (records.Count == 0) return;

        var tenantId = records[0].Context.TenantId;
        for (int i = 1; i < records.Count; i++)
        {
            if (records[i].Context.TenantId != tenantId)
            {
                throw new InvalidOperationException(
                    "All records in a batch must belong to the same tenant. " +
                    "Split cross-tenant records into separate batch operations.");
            }
        }

        using var connection = _options.ConnectionFactory();
        await SetRlsContextAsync(connection, tenantId);

        var insertSql = BuildInsertSql();
        await connection.ExecuteAsync(insertSql, records.Select(ToParameters));
    }

    /// <inheritdoc/>
    public async ValueTask<AuditQueryResult> QueryAsync(
        AuditQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (query.PageSize is < 1 or > 1000)
        {
            throw new ArgumentOutOfRangeException(nameof(query),
                "PageSize must be between 1 and 1000.");
        }

        using var connection = _options.ConnectionFactory();
        await SetRlsContextAsync(connection, query.TenantId);

        var (sql, parameters) = BuildQuerySql(query);

        var rows = await connection.QueryAsync<AuditRecordRow>(sql, parameters);

        var list = rows.ToList();
        var hasMore = list.Count > query.PageSize;
        if (hasMore) list.RemoveAt(list.Count - 1);

        var records = list.Select(MapRow).ToList();
        var nextCursor = hasMore ? records[^1].Id : (Guid?)null;

        return new AuditQueryResult(records, nextCursor, hasMore);
    }

    // ── RLS context ───────────────────────────────────────────────────────────

    private static async Task SetRlsContextAsync(IDbConnection connection, string tenantId)
    {
        if (connection.State != ConnectionState.Open)
        {
            connection.Open();
        }

        await connection.ExecuteAsync(
            "EXEC sp_set_session_context @key=N'TenantId', @value=@TenantId, @read_only=0;",
            new { TenantId = tenantId });
    }

    // ── SQL construction ─────────────────────────────────────────────────────

    private string BuildInsertSql() =>
        $"""
        INSERT INTO {_qualifiedTable} (
            [id], [occurred_at], [tenant_id], [source],
            [actor_type], [actor_id], [actor_name],
            [action_code],
            [resource_type], [resource_id], [aggregate_type], [aggregate_id],
            [outcome], [error_code],
            [correlation_id], [causation_id], [request_id], [ip_address], [user_agent],
            [changes],
            [integrity_hash], [previous_hash]
        ) VALUES (
            @Id, @OccurredAt, @TenantId, @Source,
            @ActorType, @ActorId, @ActorName,
            @ActionCode,
            @ResourceType, @ResourceId, @AggregateType, @AggregateId,
            @Outcome, @ErrorCode,
            @CorrelationId, @CausationId, @RequestId, @IpAddress, @UserAgent,
            @Changes,
            @IntegrityHash, @PreviousHash
        );
        """;

    private (string Sql, DynamicParameters Parameters) BuildQuerySql(AuditQuery query)
    {
        var where = new List<string>
        {
            "[tenant_id] = @TenantId",
            "[occurred_at] >= @MinDate"
        };

        var p = new DynamicParameters();
        p.Add("TenantId", query.TenantId);
        p.Add("MinDate", query.From?.UtcDateTime ?? DateTime.UnixEpoch);

        if (query.To.HasValue) { where.Add("[occurred_at] <= @MaxDate"); p.Add("MaxDate", query.To.Value.UtcDateTime); }
        if (query.ActorId is not null) { where.Add("[actor_id] = @ActorId"); p.Add("ActorId", query.ActorId); }
        if (query.ActionCode is not null) { where.Add("[action_code] = @ActionCode"); p.Add("ActionCode", query.ActionCode); }
        if (query.ResourceType is not null) { where.Add("[resource_type] = @ResourceType"); p.Add("ResourceType", query.ResourceType); }
        if (query.ResourceId is not null) { where.Add("[resource_id] = @ResourceId"); p.Add("ResourceId", query.ResourceId); }
        if (query.Outcome.HasValue) { where.Add("[outcome] = @Outcome"); p.Add("Outcome", (byte)query.Outcome.Value); }
        if (query.CorrelationId is not null) { where.Add("[correlation_id] = @CorrelationId"); p.Add("CorrelationId", query.CorrelationId); }

        if (query.AfterRecordId.HasValue)
        {
            where.Add($"""
                ([occurred_at] > (SELECT [occurred_at] FROM {_qualifiedTable} WHERE [id] = @CursorId)
                 OR ([occurred_at] = (SELECT [occurred_at] FROM {_qualifiedTable} WHERE [id] = @CursorId) AND [id] > @CursorId))
                """);
            p.Add("CursorId", query.AfterRecordId.Value);
        }

        var sql = $"""
            SELECT [id] AS [Id], [occurred_at] AS [OccurredAt], [tenant_id] AS [TenantId], [source] AS [Source],
                   [actor_type] AS [ActorType], [actor_id] AS [ActorId], [actor_name] AS [ActorName],
                   [action_code] AS [ActionCode],
                   [resource_type] AS [ResourceType], [resource_id] AS [ResourceId], [aggregate_type] AS [AggregateType], [aggregate_id] AS [AggregateId],
                   [outcome] AS [Outcome], [error_code] AS [ErrorCode],
                   [correlation_id] AS [CorrelationId], [causation_id] AS [CausationId], [request_id] AS [RequestId], [ip_address] AS [IpAddress], [user_agent] AS [UserAgent],
                   [changes] AS [ChangesJson],
                   [integrity_hash] AS [IntegrityHash], [previous_hash] AS [PreviousHash]
            FROM {_qualifiedTable}
            WHERE {string.Join(" AND ", where)}
            ORDER BY [occurred_at] ASC, [id] ASC
            OFFSET 0 ROWS FETCH NEXT {query.PageSize + 1} ROWS ONLY;
            """;

        return (sql, p);
    }

    // ── Mapping ───────────────────────────────────────────────────────────────

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

        return JsonSerializer.Serialize(dtos, AuditJsonContext.Default.ListAuditChangeDto);
    }

    private static AuditRecord MapRow(AuditRecordRow row)
    {
        var changes = DeserializeChanges(row.ChangesJson);

        return new AuditRecord
        {
            Id = row.Id,
            OccurredAt = row.OccurredAt,
            Actor = new AuditActor((AuditActorType)row.ActorType, row.ActorId, row.ActorName),
            Action = new AuditAction(row.ActionCode),
            Resource = new AuditResource(row.ResourceType, row.ResourceId, row.AggregateType, row.AggregateId),
            Outcome = (AuditOutcome)row.Outcome,
            ErrorCode = row.ErrorCode,
            Context = new AuditContext(
                TenantId: row.TenantId,
                Source: row.Source,
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

        var dtos = JsonSerializer.Deserialize(json, AuditJsonContext.Default.ListAuditChangeDto);
        if (dtos is null || dtos.Count == 0) return null;

        var result = new AuditChange[dtos.Count];
        for (int i = 0; i < dtos.Count; i++)
        {
            var d = dtos[i];
            result[i] = new AuditChange(d.Field, d.OldValue, d.NewValue, d.IsRedacted);
        }

        return result;
    }

    // ── Internal row DTO ─────────────────────────────────────────────────────

    private sealed class AuditRecordRow
    {
        public Guid Id { get; set; }
        public DateTimeOffset OccurredAt { get; set; }
        public string TenantId { get; set; } = null!;
        public string Source { get; set; } = null!;
        public byte ActorType { get; set; }
        public string ActorId { get; set; } = null!;
        public string? ActorName { get; set; }
        public string ActionCode { get; set; } = null!;
        public string ResourceType { get; set; } = null!;
        public string ResourceId { get; set; } = null!;
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
