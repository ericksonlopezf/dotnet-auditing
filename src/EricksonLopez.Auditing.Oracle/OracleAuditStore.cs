// Copyright © Erickson Lopez. MIT License.
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
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
    private readonly IAuditSensitivityPipeline? _sensitivityPipeline;
    private readonly string _qualifiedTable;

    /// <summary>Initializes a new instance of the <see cref="OracleAuditStore"/> class.</summary>
    /// <param name="options">The Oracle audit store configuration options.</param>
    /// <param name="sensitivityPipeline">The optional sensitivity pipeline to sanitize records before persistence.</param>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <see langword="null"/></exception>
    public OracleAuditStore(
        OracleAuditStoreOptions options,
        IAuditSensitivityPipeline? sensitivityPipeline = null)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _sensitivityPipeline = sensitivityPipeline;
        _qualifiedTable = string.IsNullOrEmpty(options.Schema)
            ? options.Table
            : $"{options.Schema}.{options.Table}";
    }

    /// <inheritdoc/>
    public async ValueTask AppendAsync(AuditRecord record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);

        if (_sensitivityPipeline is not null)
        {
            record = await _sensitivityPipeline.SanitizeAsync(record, cancellationToken).ConfigureAwait(false);
        }

        using var connection = _options.ConnectionFactory();
        try
        {
            await SetSessionContextAsync(connection, record.Context.TenantId, cancellationToken).ConfigureAwait(false);

            var cmd = new CommandDefinition(BuildInsertSql(), ToParameters(record), cancellationToken: cancellationToken);
            await connection.ExecuteAsync(cmd).ConfigureAwait(false);
        }
        finally
        {
            await ClearSessionContextAsync(connection).ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    public async ValueTask AppendBatchAsync(
        IReadOnlyList<AuditRecord> records,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(records);
        if (records.Count == 0) return;

        if (_sensitivityPipeline is not null)
        {
            var sanitized = new List<AuditRecord>(records.Count);
            for (int i = 0; i < records.Count; i++)
            {
                sanitized.Add(await _sensitivityPipeline.SanitizeAsync(records[i], cancellationToken).ConfigureAwait(false));
            }
            records = sanitized;
        }

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
        await OpenConnectionAsync(connection, cancellationToken).ConfigureAwait(false);
        try
        {
            using var transaction = await BeginTransactionAsync(connection, cancellationToken).ConfigureAwait(false);
            await SetSessionContextAsync(connection, transaction, tenantId, cancellationToken).ConfigureAwait(false);

            var insertSql = BuildInsertSql();
            var cmd = new CommandDefinition(insertSql, records.Select(ToParameters), transaction: transaction, cancellationToken: cancellationToken);
            await connection.ExecuteAsync(cmd).ConfigureAwait(false);
            CommitTransaction(transaction);
        }
        finally
        {
            await ClearSessionContextAsync(connection).ConfigureAwait(false);
        }
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
        try
        {
            await SetSessionContextAsync(connection, query.TenantId, cancellationToken).ConfigureAwait(false);

            var (sql, parameters) = BuildQuerySql(query);

            var cmd = new CommandDefinition(sql, parameters, cancellationToken: cancellationToken);
            var rows = await connection.QueryAsync<AuditRecordRow>(cmd).ConfigureAwait(false);

            var list = rows.ToList();
            var hasMore = list.Count > query.PageSize;
            if (hasMore) list.RemoveAt(list.Count - 1);

            var records = list.Select(MapRow).ToList();
            var nextCursor = hasMore ? AuditCursorToken.Create(records[^1].OccurredAt, records[^1].Id) : null;

            return new AuditQueryResult(records, nextCursor, hasMore);
        }
        finally
        {
            await ClearSessionContextAsync(connection).ConfigureAwait(false);
        }
    }

    // ── Session context ───────────────────────────────────────────────────────

    private static Task SetSessionContextAsync(IDbConnection connection, string tenantId, CancellationToken cancellationToken) =>
        SetSessionContextAsync(connection, null, tenantId, cancellationToken);

    private static async Task SetSessionContextAsync(IDbConnection connection, IDbTransaction? transaction, string tenantId, CancellationToken cancellationToken)
    {
        await OpenConnectionAsync(connection, cancellationToken).ConfigureAwait(false);

        var cmd = new CommandDefinition(
            "BEGIN DBMS_SESSION.SET_IDENTIFIER(:TenantId); END;",
            new { TenantId = tenantId },
            transaction: transaction,
            cancellationToken: cancellationToken);
        await connection.ExecuteAsync(cmd).ConfigureAwait(false);
    }

    private static async Task ClearSessionContextAsync(IDbConnection connection)
    {
        if (connection.State != ConnectionState.Open) return;

        var cmd = new CommandDefinition(
            "BEGIN DBMS_SESSION.CLEAR_IDENTIFIER(); END;",
            cancellationToken: default);
        await connection.ExecuteAsync(cmd).ConfigureAwait(false);
    }

    private static async Task OpenConnectionAsync(IDbConnection connection, CancellationToken cancellationToken)
    {
        if (connection.State != ConnectionState.Open)
        {
            if (connection is System.Data.Common.DbConnection dbConn)
            {
                await dbConn.OpenAsync(cancellationToken).ConfigureAwait(false);
            }
            else
            {
                connection.Open();
            }
        }
    }

    private static async Task<IDbTransaction> BeginTransactionAsync(IDbConnection connection, CancellationToken cancellationToken)
    {
        if (connection is System.Data.Common.DbConnection dbConn)
        {
            return await dbConn.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        }

        return connection.BeginTransaction();
    }

    private static void CommitTransaction(IDbTransaction transaction)
    {
        transaction.Commit();
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
            "CORRELATION_ID", "CAUSATION_ID", "REQUEST_ID", "IP_ADDRESS", "USER_AGENT", "IDEMPOTENCY_KEY",
            "CHANGES",
            "INTEGRITY_HASH", "PREVIOUS_HASH"
        ) VALUES (
            :Id, :OccurredAt, :TenantId, :Source,
            :ActorType, :ActorId, :ActorName,
            :ActionCode,
            :ResourceType, :ResourceId, :AggregateType, :AggregateId,
            :Outcome, :ErrorCode,
            :CorrelationId, :CausationId, :RequestId, :IpAddress, :UserAgent, :IdempotencyKey,
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
        p.Add("TenantId", query.TenantId.Value);
        p.Add("StartDate", query.From ?? DateTimeOffset.UnixEpoch);

        if (query.To.HasValue) { where.Add("\"OCCURRED_AT\" <= :EndDate"); p.Add("EndDate", query.To.Value); }
        if (query.ActorId is not null) { where.Add("\"ACTOR_ID\" = :ActorId"); p.Add("ActorId", query.ActorId); }
        if (query.ActionCode is not null) { where.Add("\"ACTION_CODE\" = :ActionCode"); p.Add("ActionCode", query.ActionCode); }
        if (query.ResourceType is not null) { where.Add("\"RESOURCE_TYPE\" = :ResourceType"); p.Add("ResourceType", query.ResourceType); }
        if (query.ResourceId is not null) { where.Add("\"RESOURCE_ID\" = :ResourceId"); p.Add("ResourceId", query.ResourceId); }
        if (query.Outcome.HasValue) { where.Add("\"OUTCOME\" = :Outcome"); p.Add("Outcome", (byte)query.Outcome.Value); }
        if (query.CorrelationId is not null) { where.Add("\"CORRELATION_ID\" = :CorrelationId"); p.Add("CorrelationId", query.CorrelationId); }

        // Keyset pagination: tuple-style comparison using AND/OR
        if (AuditCursorToken.TryParse(query.ContinuationToken, out var cursorDate, out var cursorId))
        {
            where.Add($"""
                ("OCCURRED_AT" > TO_TIMESTAMP(:CursorDate, 'YYYY-MM-DD HH24:MI:SS.FF6')
                 OR ("OCCURRED_AT" = TO_TIMESTAMP(:CursorDate, 'YYYY-MM-DD HH24:MI:SS.FF6') AND "ID" > :CursorId))
                """);
            p.Add("CursorDate", cursorDate.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss.ffffff", CultureInfo.InvariantCulture));
            p.Add("CursorId", cursorId.ToString("N").ToUpperInvariant());
        }

        var sql = $"""
            SELECT "ID", "OCCURRED_AT", "TENANT_ID", "SOURCE",
                   "ACTOR_TYPE", "ACTOR_ID", "ACTOR_NAME",
                   "ACTION_CODE",
                   "RESOURCE_TYPE", "RESOURCE_ID", "AGGREGATE_TYPE", "AGGREGATE_ID",
                   "OUTCOME", "ERROR_CODE",
                   "CORRELATION_ID", "CAUSATION_ID", "REQUEST_ID", "IP_ADDRESS", "USER_AGENT", "IDEMPOTENCY_KEY",
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
        p.Add("TenantId", record.Context.TenantId.Value);
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
        p.Add("IdempotencyKey", record.Context.IdempotencyKey);
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
                UserAgent: row.USER_AGENT,
                IdempotencyKey: row.IDEMPOTENCY_KEY),
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

    [SuppressMessage("Minor Code Smell", "S3459:Unassigned auto-property", Justification = "Instantiated and mapped dynamically by Dapper.")]
    [SuppressMessage("Major Code Smell", "S1144:Unused private types or members", Justification = "Instantiated and mapped dynamically by Dapper.")]
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
        public string? IDEMPOTENCY_KEY { get; set; }
        public string? CHANGES_JSON { get; set; }
        public string? INTEGRITY_HASH { get; set; }
        public string? PREVIOUS_HASH { get; set; }
    }
}

