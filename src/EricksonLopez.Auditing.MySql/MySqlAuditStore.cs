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

namespace EricksonLopez.Auditing.MySql;

/// <summary>Provides a MySQL and MariaDB persistence store for immutable audit records using Dapper.</summary>
public sealed class MySqlAuditStore : IAuditStore
{
    private readonly MySqlAuditStoreOptions _options;
    private readonly IAuditSensitivityPipeline? _sensitivityPipeline;

    /// <summary>Initializes a new instance of the <see cref="MySqlAuditStore"/> class.</summary>
    /// <param name="options">The MySQL audit store configuration options.</param>
    /// <param name="sensitivityPipeline">The optional sensitivity pipeline to sanitize records before persistence.</param>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <see langword="null"/></exception>
    public MySqlAuditStore(
        MySqlAuditStoreOptions options,
        IAuditSensitivityPipeline? sensitivityPipeline = null)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
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
            "SET @audit_tenant_id = @TenantId;",
            new { TenantId = tenantId },
            transaction: transaction,
            cancellationToken: cancellationToken);
        await connection.ExecuteAsync(cmd).ConfigureAwait(false);
    }

    private static async Task ClearSessionContextAsync(IDbConnection connection)
    {
        if (connection.State != ConnectionState.Open) return;

        var cmd = new CommandDefinition(
            "SET @audit_tenant_id = NULL;",
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
        INSERT INTO `{_options.Table}` (
            `id`, `occurred_at`, `tenant_id`, `source`,
            `actor_type`, `actor_id`, `actor_name`,
            `action_code`,
            `resource_type`, `resource_id`, `aggregate_type`, `aggregate_id`,
            `outcome`, `error_code`,
            `correlation_id`, `causation_id`, `request_id`, `ip_address`, `user_agent`, `idempotency_key`,
            `changes`,
            `integrity_hash`, `previous_hash`
        ) VALUES (
            @Id, @OccurredAt, @TenantId, @Source,
            @ActorType, @ActorId, @ActorName,
            @ActionCode,
            @ResourceType, @ResourceId, @AggregateType, @AggregateId,
            @Outcome, @ErrorCode,
            @CorrelationId, @CausationId, @RequestId, @IpAddress, @UserAgent, @IdempotencyKey,
            @Changes,
            @IntegrityHash, @PreviousHash
        );
        """;

    private (string Sql, DynamicParameters Parameters) BuildQuerySql(AuditQuery query)
    {
        var where = new List<string>
        {
            "`tenant_id` = @TenantId",
            "`occurred_at` >= @MinDate"
        };

        var p = new DynamicParameters();
        p.Add("TenantId", query.TenantId.Value);
        p.Add("MinDate", query.From?.UtcDateTime ?? DateTime.UnixEpoch);

        if (query.To.HasValue) { where.Add("`occurred_at` <= @MaxDate"); p.Add("MaxDate", query.To.Value.UtcDateTime); }
        if (query.ActorId is not null) { where.Add("`actor_id` = @ActorId"); p.Add("ActorId", query.ActorId); }
        if (query.ActionCode is not null) { where.Add("`action_code` = @ActionCode"); p.Add("ActionCode", query.ActionCode); }
        if (query.ResourceType is not null) { where.Add("`resource_type` = @ResourceType"); p.Add("ResourceType", query.ResourceType); }
        if (query.ResourceId is not null) { where.Add("`resource_id` = @ResourceId"); p.Add("ResourceId", query.ResourceId); }
        if (query.Outcome.HasValue) { where.Add("`outcome` = @Outcome"); p.Add("Outcome", (byte)query.Outcome.Value); }
        if (query.CorrelationId is not null) { where.Add("`correlation_id` = @CorrelationId"); p.Add("CorrelationId", query.CorrelationId); }

        // Keyset pagination: tuple-style comparison using AND/OR
        if (AuditCursorToken.TryParse(query.ContinuationToken, out var cursorDate, out var cursorId))
        {
            where.Add($"""
                (occurred_at > @CursorDate
                 OR (occurred_at = @CursorDate AND id > @CursorId))
                """);
            p.Add("CursorDate", cursorDate.ToString("yyyy-MM-dd HH:mm:ss.ffffff", CultureInfo.InvariantCulture));
            p.Add("CursorId", cursorId.ToString());
        }

        var sql = $"""
            SELECT `id` AS Id, `occurred_at` AS OccurredAt, `tenant_id` AS TenantId, `source` AS Source,
                   `actor_type` AS ActorType, `actor_id` AS ActorId, `actor_name` AS ActorName,
                   `action_code` AS ActionCode,
                   `resource_type` AS ResourceType, `resource_id` AS ResourceId, `aggregate_type` AS AggregateType, `aggregate_id` AS AggregateId,
                   `outcome` AS Outcome, `error_code` AS ErrorCode,
                   `correlation_id` AS CorrelationId, `causation_id` AS CausationId, `request_id` AS RequestId, `ip_address` AS IpAddress, `user_agent` AS UserAgent, `idempotency_key` AS IdempotencyKey,
                   `changes` AS ChangesJson,
                   `integrity_hash` AS IntegrityHash, `previous_hash` AS PreviousHash
            FROM `{_options.Table}`
            WHERE {string.Join(" AND ", where)}
            ORDER BY `occurred_at` ASC, `id` ASC
            LIMIT {query.PageSize + 1};
            """;

        return (sql, p);
    }

    // ── Mapping ───────────────────────────────────────────────────────────────

    private static DynamicParameters ToParameters(AuditRecord record)
    {
        var p = new DynamicParameters();
        p.Add("Id", record.Id.ToString());
        p.Add("OccurredAt", record.OccurredAt.UtcDateTime);
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
        var changes = DeserializeChanges(row.ChangesJson);

        return new AuditRecord
        {
            Id = Guid.Parse(row.Id),
            OccurredAt = new DateTimeOffset(DateTime.SpecifyKind(row.OccurredAt, DateTimeKind.Utc), TimeSpan.Zero),
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
                UserAgent: row.UserAgent,
                IdempotencyKey: row.IdempotencyKey),
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

    [SuppressMessage("Minor Code Smell", "S3459:Unassigned auto-property", Justification = "Instantiated and mapped dynamically by Dapper.")]
    [SuppressMessage("Major Code Smell", "S1144:Unused private types or members", Justification = "Instantiated and mapped dynamically by Dapper.")]
    private sealed class AuditRecordRow
    {
        public string Id { get; set; } = null!;
        public DateTime OccurredAt { get; set; }
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
        public string? IdempotencyKey { get; set; }
        public string? ChangesJson { get; set; }
        public string? IntegrityHash { get; set; }
        public string? PreviousHash { get; set; }
    }
}

