// Copyright © Erickson Lopez. MIT License.
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Dapper;

namespace EricksonLopez.Auditing.PostgreSql;

/// <summary>Verifies the cryptographic HMAC chain for audit records stored in PostgreSQL.</summary>
public sealed class PostgreSqlAuditIntegrityVerifier : IAuditIntegrityVerifier
{
    private readonly PostgreSqlAuditStoreOptions _options;
    private readonly HmacAuditIntegrityService _hmac;

    /// <summary>Initializes a new instance of the <see cref="PostgreSqlAuditIntegrityVerifier"/> class.</summary>
    /// <param name="options">The PostgreSQL audit store configuration options.</param>
    /// <param name="hmac">The HMAC integrity service used to evaluate record hashes.</param>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> or <paramref name="hmac"/> is <see langword="null"/></exception>
    public PostgreSqlAuditIntegrityVerifier(
        PostgreSqlAuditStoreOptions options,
        HmacAuditIntegrityService hmac)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _hmac = hmac ?? throw new ArgumentNullException(nameof(hmac));
    }

    /// <inheritdoc/>
    [SuppressMessage("Security", "S2077:Use a parameterized query instead of string formatting.", Justification = "Schema and table names are configured identifiers that cannot be parameterized in SQL.")]
    public async ValueTask<AuditIntegrityVerificationResult> VerifyChainAsync(
        string tenantId,
        DateTimeOffset from,
        DateTimeOffset until,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(tenantId);

        using var connection = _options.ConnectionFactory();
        if (connection.State != ConnectionState.Open) connection.Open();

        await connection.ExecuteAsync(
            "SELECT set_config('audit.tenant_id', @TenantId, false);",
            new { TenantId = tenantId });

        var sql = $"""
            SELECT id AS "Id", occurred_at AS "OccurredAt", tenant_id AS "TenantId", source AS "Source",
                   actor_type AS "ActorType", actor_id AS "ActorId", actor_name AS "ActorName",
                   action_code AS "ActionCode",
                   resource_type AS "ResourceType", resource_id AS "ResourceId", aggregate_type AS "AggregateType", aggregate_id AS "AggregateId",
                   outcome AS "Outcome", error_code AS "ErrorCode",
                   correlation_id AS "CorrelationId", causation_id AS "CausationId", request_id AS "RequestId", ip_address::text AS "IpAddress", user_agent AS "UserAgent",
                   integrity_hash AS "IntegrityHash", previous_hash AS "PreviousHash"
            FROM {_options.Schema}.{_options.Table}
            WHERE tenant_id = @TenantId
              AND occurred_at >= @From
              AND occurred_at <= @To
            ORDER BY occurred_at ASC, id ASC
            """;

        var rows = await connection.QueryAsync<IntegrityRow>(sql, new
        {
            TenantId = tenantId,
            From = from,
            To = until
        });

        int count = 0;
        string? expectedPreviousHash = null;

        foreach (var row in rows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            count++;

            // Reconstruct the minimal record for hash verification (no changes needed)
            var record = new AuditRecord
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
                IntegrityHash = row.IntegrityHash,
                PreviousHash = row.PreviousHash
            };

            // Check chain continuity: stored previous_hash must match predecessor's hash
            if (count > 1 && row.PreviousHash != expectedPreviousHash)
            {
                return new AuditIntegrityVerificationResult(
                    IsValid: false,
                    VerifiedCount: count,
                    FirstFailedRecordId: row.Id,
                    FailureReason: "Chain break: previous_hash does not match predecessor's integrity_hash.");
            }

            // Verify the record's own hash
            if (!_hmac.Verify(record))
            {
                return new AuditIntegrityVerificationResult(
                    IsValid: false,
                    VerifiedCount: count,
                    FirstFailedRecordId: row.Id,
                    FailureReason: "Integrity hash mismatch: record content has been tampered with.");
            }

            expectedPreviousHash = row.IntegrityHash;
        }

        return new AuditIntegrityVerificationResult(IsValid: true, VerifiedCount: count);
    }

    // Typed row DTO — eliminates dynamic, AOT-safe
    [SuppressMessage("Minor Code Smell", "S3459:Unassigned auto-property", Justification = "Instantiated and mapped dynamically by Dapper.")]
    [SuppressMessage("Major Code Smell", "S1144:Unused private types or members", Justification = "Instantiated and mapped dynamically by Dapper.")]
    private sealed class IntegrityRow
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
        public string? IntegrityHash { get; set; }
        public string? PreviousHash { get; set; }
    }
}
