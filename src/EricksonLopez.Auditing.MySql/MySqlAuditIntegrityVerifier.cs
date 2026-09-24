// Copyright © Erickson Lopez. MIT License.
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Dapper;

namespace EricksonLopez.Auditing.MySql;

/// <summary>Provides cryptographic HMAC chain verification for audit records stored in MySQL or MariaDB.</summary>
public sealed class MySqlAuditIntegrityVerifier : IAuditIntegrityVerifier
{
    private readonly MySqlAuditStoreOptions _options;
    private readonly HmacAuditIntegrityService _hmac;

    /// <summary>Initializes a new instance of the <see cref="MySqlAuditIntegrityVerifier"/> class.</summary>
    /// <param name="options">The MySQL audit store configuration options.</param>
    /// <param name="hmac">The HMAC integrity service used to evaluate record hashes.</param>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> or <paramref name="hmac"/> is <see langword="null"/></exception>
    public MySqlAuditIntegrityVerifier(
        MySqlAuditStoreOptions options,
        HmacAuditIntegrityService hmac)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _hmac = hmac ?? throw new ArgumentNullException(nameof(hmac));
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentException"><paramref name="tenantId"/> is <see langword="null"/> or empty</exception>
    [SuppressMessage("Security", "S2077:Use a parameterized query instead of string formatting.", Justification = "Table name is a configured identifier that cannot be parameterized in SQL.")]
    public async ValueTask<AuditIntegrityVerificationResult> VerifyChainAsync(
        string tenantId,
        DateTimeOffset from,
        DateTimeOffset until,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(tenantId);

        using var connection = _options.ConnectionFactory();
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

        var sql = $"""
            SELECT `id` AS Id, `occurred_at` AS OccurredAt, `tenant_id` AS TenantId, `source` AS Source,
                   `actor_type` AS ActorType, `actor_id` AS ActorId, `actor_name` AS ActorName,
                   `action_code` AS ActionCode,
                   `resource_type` AS ResourceType, `resource_id` AS ResourceId, `aggregate_type` AS AggregateType, `aggregate_id` AS AggregateId,
                   `outcome` AS Outcome, `error_code` AS ErrorCode,
                   `correlation_id` AS CorrelationId, `causation_id` AS CausationId, `request_id` AS RequestId, `ip_address` AS IpAddress, `user_agent` AS UserAgent,
                   `changes` AS ChangesJson,
                   `integrity_hash` AS IntegrityHash, `previous_hash` AS PreviousHash
            FROM `{_options.Table}`
            WHERE `tenant_id` = @TenantId
              AND `occurred_at` >= @From
              AND `occurred_at` <= @To
            ORDER BY `occurred_at` ASC, `id` ASC;
            """;

        var cmd = new CommandDefinition(sql, new
        {
            TenantId = tenantId,
            From = from.UtcDateTime,
            To = until.UtcDateTime
        }, cancellationToken: cancellationToken);

        var rows = await connection.QueryAsync<IntegrityRow>(cmd).ConfigureAwait(false);

        int count = 0;
        string? expectedPreviousHash = null;

        foreach (var row in rows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            count++;

            var changes = DeserializeChanges(row.ChangesJson);

            var record = new AuditRecord
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
                    UserAgent: row.UserAgent),
                Changes = changes,
                IntegrityHash = row.IntegrityHash,
                PreviousHash = row.PreviousHash
            };

            if (count > 1 && row.PreviousHash != expectedPreviousHash)
            {
                return new AuditIntegrityVerificationResult(
                    IsValid: false,
                    VerifiedCount: count,
                    FirstFailedRecordId: record.Id,
                    FailureReason: "Chain break: previous_hash does not match predecessor's integrity_hash.");
            }

            if (!_hmac.Verify(record))
            {
                return new AuditIntegrityVerificationResult(
                    IsValid: false,
                    VerifiedCount: count,
                    FirstFailedRecordId: record.Id,
                    FailureReason: "Integrity hash mismatch: record content has been tampered with.");
            }

            expectedPreviousHash = row.IntegrityHash;
        }

        return new AuditIntegrityVerificationResult(IsValid: true, VerifiedCount: count);
    }

    private static AuditChange[]? DeserializeChanges(string? json)
    {
        if (string.IsNullOrEmpty(json)) return null;

        var dtos = System.Text.Json.JsonSerializer.Deserialize(json, AuditJsonContext.Default.ListAuditChangeDto);
        if (dtos is null || dtos.Count == 0) return null;

        var result = new AuditChange[dtos.Count];
        for (int i = 0; i < dtos.Count; i++)
        {
            var d = dtos[i];
            result[i] = new AuditChange(d.Field, d.OldValue, d.NewValue, d.IsRedacted);
        }

        return result;
    }

    [SuppressMessage("Minor Code Smell", "S3459:Unassigned auto-property", Justification = "Instantiated and mapped dynamically by Dapper.")]
    [SuppressMessage("Major Code Smell", "S1144:Unused private types or members", Justification = "Instantiated and mapped dynamically by Dapper.")]
    private sealed class IntegrityRow
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
        public string? ChangesJson { get; set; }
        public string? IntegrityHash { get; set; }
        public string? PreviousHash { get; set; }
    }
}
