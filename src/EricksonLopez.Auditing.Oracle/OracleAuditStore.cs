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

namespace EricksonLopez.Auditing.Oracle;

/// <summary>Provides an Oracle Database persistence store for immutable audit records using Dapper.</summary>
public sealed class OracleAuditStore : IAuditStore
{
    private readonly OracleAuditStoreOptions _options;
    private readonly string _qualifiedTable;

    /// <summary>Initializes a new instance of the <see cref="OracleAuditStore"/> class.</summary>
    /// <param name="options">The Oracle audit store configuration options.</param>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <see langword="null"/></exception>
    public OracleAuditStore(OracleAuditStoreOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _qualifiedTable = string.IsNullOrEmpty(options.Schema)
            ? options.Table
            : $"{options.Schema}.{options.Table}";
    }

    /// <inheritdoc/>
    public async ValueTask AppendAsync(AuditRecord record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);

        using var connection = _options.ConnectionFactory();
        await SetSessionContextAsync(connection, record.Context.TenantId);

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
        await SetSessionContextAsync(connection, tenantId);

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
        await SetSessionContextAsync(connection, query.TenantId);

        var (sql, parameters) = BuildQuerySql(query);

        var rows = await connection.QueryAsync<AuditRecordRow>(sql, parameters);

        var list = rows.ToList();
        var hasMore = list.Count > query.PageSize;
        if (hasMore) list.RemoveAt(list.Count - 1);

        var records = list.Select(MapRow).ToList();
        var nextCursor = hasMore ? records[^1].Id : (Guid?)null;

        return new AuditQueryResult(records, nextCursor, hasMore);
    }

    // ── Session context ───────────────────────────────────────────────────────

    private static async Task SetSessionContextAsync(IDbConnection connection, string tenantId)
    {
        if (connection.State != ConnectionState.Open)
        {
            connection.Open();
        }

        await connection.ExecuteAsync(
            "BEGIN DBMS_SESSION.SET_IDENTIFIER(:TenantId); END;",
            new { TenantId = tenantId });
    }

    // ── SQL construction ─────────────────────────────────────────────────────

    private string BuildInsertSql() =>
        $"""
        INSERT INTO {_qualifiedTable} (
            "ID", "OCCURRED_AT", "TENANT_ID", "SOURCE",
            "ACTOR_TYPE", "ACTOR_ID", "ACTOR_NAME",
            "ACTION_CODE",
            "RESOURCE_TYPE", "RESOURCE_ID", "AGGREGATE_TYPE", "AGGREGATE_ID",
            "OUTCOME", "ERROR_CODE",
            "CORRELATION_ID", "CAUSATION_ID", "REQUEST_ID", "IP_ADDRESS", "USER_AGENT",
            "CHANGES",
            "INTEGRITY_HASH", "PREVIOUS_HASH"
        ) VALUES (
            :Id, :OccurredAt, :TenantId, :Source,
            :ActorType, :ActorId, :ActorName,
            :ActionCode,
            :ResourceType, :ResourceId, :AggregateType, :AggregateId,
            :Outcome, :ErrorCode,
            :CorrelationId, :CausationId, :RequestId, :IpAddress, :UserAgent,
            :Changes,
            :IntegrityHash, :PreviousHash
        )
        """;

    private (string Sql, DynamicParameters Parameters) BuildQuerySql(AuditQuery query)
    {
        var where = new List<string>
        {
            "\"TENANT_ID\" = :TenantId",
            "\"OCCURRED_AT\" >= :StartDate"
        };

        var p = new DynamicParameters();
        p.Add("TenantId", query.TenantId);
        p.Add("StartDate", query.From ?? DateTimeOffset.UnixEpoch);

        if (query.To.HasValue) { where.Add("\"OCCURRED_AT\" <= :EndDate"); p.Add("EndDate", query.To.Value); }
        if (query.ActorId is not null) { where.Add("\"ACTOR_ID\" = :ActorId"); p.Add("ActorId", query.ActorId); }
        if (query.ActionCode is not null) { where.Add("\"ACTION_CODE\" = :ActionCode"); p.Add("ActionCode", query.ActionCode); }
        if (query.ResourceType is not null) { where.Add("\"RESOURCE_TYPE\" = :ResourceType"); p.Add("ResourceType", query.ResourceType); }
        if (query.ResourceId is not null) { where.Add("\"RESOURCE_ID\" = :ResourceId"); p.Add("ResourceId", query.ResourceId); }
        if (query.Outcome.HasValue) { where.Add("\"OUTCOME\" = :Outcome"); p.Add("Outcome", (byte)query.Outcome.Value); }
        if (query.CorrelationId is not null) { where.Add("\"CORRELATION_ID\" = :CorrelationId"); p.Add("CorrelationId", query.CorrelationId); }

        if (query.AfterRecordId.HasValue)
        {
            var cursorGuidStr = query.AfterRecordId.Value.ToString();
            where.Add($"""
                ("OCCURRED_AT" > (SELECT "OCCURRED_AT" FROM {_qualifiedTable} WHERE "ID" = :CursorId)
                 OR ("OCCURRED_AT" = (SELECT "OCCURRED_AT" FROM {_qualifiedTable} WHERE "ID" = :CursorId) AND "ID" > :CursorId))
                """);
            p.Add("CursorId", cursorGuidStr);
        }

        var sql = $"""
            SELECT "ID", "OCCURRED_AT", "TENANT_ID", "SOURCE",
                   "ACTOR_TYPE", "ACTOR_ID", "ACTOR_NAME",
                   "ACTION_CODE",
                   "RESOURCE_TYPE", "RESOURCE_ID", "AGGREGATE_TYPE", "AGGREGATE_ID",
                   "OUTCOME", "ERROR_CODE",
                   "CORRELATION_ID", "CAUSATION_ID", "REQUEST_ID", "IP_ADDRESS", "USER_AGENT",
                   "CHANGES" AS "CHANGES_JSON",
                   "INTEGRITY_HASH", "PREVIOUS_HASH"
            FROM {_qualifiedTable}
            WHERE {string.Join(" AND ", where)}
            ORDER BY "OCCURRED_AT" ASC, "ID" ASC
            FETCH FIRST {query.PageSize + 1} ROWS ONLY
            """;

        return (sql, p);
    }

    // ── Mapping ───────────────────────────────────────────────────────────────

    private static DynamicParameters ToParameters(AuditRecord record)
    {
        var p = new DynamicParameters();
        p.Add("Id", record.Id.ToString());
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
        var changes = DeserializeChanges(row.CHANGES_JSON);

        return new AuditRecord
        {
            Id = Guid.Parse(row.ID),
            OccurredAt = row.OCCURRED_AT,
            Actor = new AuditActor((AuditActorType)row.ACTOR_TYPE, row.ACTOR_ID, row.ACTOR_NAME),
            Action = new AuditAction(row.ACTION_CODE),
            Resource = new AuditResource(row.RESOURCE_TYPE, row.RESOURCE_ID, row.AGGREGATE_TYPE, row.AGGREGATE_ID),
            Outcome = (AuditOutcome)row.OUTCOME,
            ErrorCode = row.ERROR_CODE,
            Context = new AuditContext(
                TenantId: row.TENANT_ID,
                Source: row.SOURCE,
                CorrelationId: row.CORRELATION_ID,
                CausationId: row.CAUSATION_ID,
                RequestId: row.REQUEST_ID,
                IpAddress: row.IP_ADDRESS,
                UserAgent: row.USER_AGENT),
            Changes = changes,
            IntegrityHash = row.INTEGRITY_HASH,
            PreviousHash = row.PREVIOUS_HASH
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
        public string ID { get; set; } = null!;
        public DateTimeOffset OCCURRED_AT { get; set; }
        public string TENANT_ID { get; set; } = null!;
        public string SOURCE { get; set; } = null!;
        public byte ACTOR_TYPE { get; set; }
        public string ACTOR_ID { get; set; } = null!;
        public string? ACTOR_NAME { get; set; }
        public string ACTION_CODE { get; set; } = null!;
        public string RESOURCE_TYPE { get; set; } = null!;
        public string RESOURCE_ID { get; set; } = null!;
        public string? AGGREGATE_TYPE { get; set; }
        public string? AGGREGATE_ID { get; set; }
        public byte OUTCOME { get; set; }
        public string? ERROR_CODE { get; set; }
        public string? CORRELATION_ID { get; set; }
        public string? CAUSATION_ID { get; set; }
        public string? REQUEST_ID { get; set; }
        public string? IP_ADDRESS { get; set; }
        public string? USER_AGENT { get; set; }
        public string? CHANGES_JSON { get; set; }
        public string? INTEGRITY_HASH { get; set; }
        public string? PREVIOUS_HASH { get; set; }
    }
}
