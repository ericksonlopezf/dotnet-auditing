// Copyright © Erickson Lopez. MIT License.
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Dapper;

namespace EricksonLopez.Auditing.Sqlite;

/// <summary>Verifies the cryptographic HMAC chain for audit records stored in SQLite.</summary>
public sealed class SqliteAuditIntegrityVerifier : IAuditIntegrityVerifier
{
    private readonly SqliteAuditStoreOptions _options;
    private readonly HmacAuditIntegrityService _hmac;

    /// <summary>Initializes a new instance of the <see cref="SqliteAuditIntegrityVerifier"/> class.</summary>
    /// <param name="options">The SQLite audit store configuration options.</param>
    /// <param name="hmac">The HMAC integrity service used to evaluate record hashes.</param>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> or <paramref name="hmac"/> is <see langword="null"/></exception>
    public SqliteAuditIntegrityVerifier(
        SqliteAuditStoreOptions options,
        HmacAuditIntegrityService hmac)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _hmac = hmac ?? throw new ArgumentNullException(nameof(hmac));
    }

    /// <inheritdoc/>
    public async ValueTask<AuditIntegrityVerificationResult> VerifyChainAsync(
        string tenantId,
        DateTimeOffset from,
        DateTimeOffset until,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(tenantId);

        using var connection = _options.ConnectionFactory();
        var sql = $"""
            SELECT id AS Id, occurred_at AS OccurredAt, tenant_id AS TenantId, source AS Source,
                   actor_type AS ActorType, actor_id AS ActorId, actor_name AS ActorName,
                   action_code AS ActionCode,
                   resource_type AS ResourceType, resource_id AS ResourceId, aggregate_type AS AggregateType, aggregate_id AS AggregateId,
                   outcome AS Outcome, error_code AS ErrorCode,
                   correlation_id AS CorrelationId, causation_id AS CausationId, request_id AS RequestId, ip_address AS IpAddress, user_agent AS UserAgent,
                   integrity_hash AS IntegrityHash, previous_hash AS PreviousHash
            FROM {_options.Table}
            WHERE tenant_id = @TenantId
              AND occurred_at >= @From
              AND occurred_at <= @To
            ORDER BY occurred_at ASC, id ASC;
            """;

        var rows = await connection.QueryAsync<IntegrityRow>(sql, new
        {
            TenantId = tenantId,
            From = from.ToString("O", CultureInfo.InvariantCulture),
            To = until.ToString("O", CultureInfo.InvariantCulture)
        });

        int count = 0;
        string? expectedPreviousHash = null;

        foreach (var row in rows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            count++;

            var record = new AuditRecord
            {
                Id = Guid.Parse(row.Id),
                OccurredAt = DateTimeOffset.Parse(row.OccurredAt, CultureInfo.InvariantCulture),
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

    private sealed class IntegrityRow
    {
        public string Id { get; set; } = null!;
        public string OccurredAt { get; set; } = null!;
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
