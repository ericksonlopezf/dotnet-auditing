// Copyright © Erickson Lopez. MIT License.
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Dapper;

namespace EricksonLopez.Auditing.Oracle;

/// <summary>Verifies the cryptographic HMAC chain for audit records stored in Oracle Database.</summary>
public sealed class OracleAuditIntegrityVerifier : IAuditIntegrityVerifier
{
    private readonly OracleAuditStoreOptions _options;
    private readonly HmacAuditIntegrityService _hmac;

    /// <summary>Initializes a new instance of the <see cref="OracleAuditIntegrityVerifier"/> class.</summary>
    /// <param name="options">The Oracle audit store configuration options.</param>
    /// <param name="hmac">The HMAC integrity service used to evaluate record hashes.</param>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> or <paramref name="hmac"/> is <see langword="null"/></exception>
    public OracleAuditIntegrityVerifier(
        OracleAuditStoreOptions options,
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
            "BEGIN DBMS_SESSION.SET_IDENTIFIER(:TenantId); END;",
            new { TenantId = tenantId });

        var tableRef = string.IsNullOrEmpty(_options.Schema)
            ? _options.Table
            : $"{_options.Schema}.{_options.Table}";

        var sql = $"""
            SELECT "ID", "OCCURRED_AT", "TENANT_ID", "SOURCE",
                   "ACTOR_TYPE", "ACTOR_ID", "ACTOR_NAME",
                   "ACTION_CODE",
                   "RESOURCE_TYPE", "RESOURCE_ID", "AGGREGATE_TYPE", "AGGREGATE_ID",
                   "OUTCOME", "ERROR_CODE",
                   "CORRELATION_ID", "CAUSATION_ID", "REQUEST_ID", "IP_ADDRESS", "USER_AGENT",
                   "INTEGRITY_HASH", "PREVIOUS_HASH"
            FROM {tableRef}
            WHERE "TENANT_ID" = :TenantId
              AND "OCCURRED_AT" >= :StartDate
              AND "OCCURRED_AT" <= :EndDate
            ORDER BY "OCCURRED_AT" ASC, "ID" ASC
            """;

        var rows = await connection.QueryAsync<IntegrityRow>(sql, new
        {
            TenantId = tenantId,
            StartDate = from,
            EndDate = until
        });

        int count = 0;
        string? expectedPreviousHash = null;

        foreach (var row in rows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            count++;

            var record = new AuditRecord
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
                IntegrityHash = row.INTEGRITY_HASH,
                PreviousHash = row.PREVIOUS_HASH
            };

            if (count > 1 && row.PREVIOUS_HASH != expectedPreviousHash)
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

            expectedPreviousHash = row.INTEGRITY_HASH;
        }

        return new AuditIntegrityVerificationResult(IsValid: true, VerifiedCount: count);
    }

    [SuppressMessage("Minor Code Smell", "S3459:Unassigned auto-property", Justification = "Instantiated and mapped dynamically by Dapper.")]
    [SuppressMessage("Major Code Smell", "S1144:Unused private types or members", Justification = "Instantiated and mapped dynamically by Dapper.")]
    private sealed class IntegrityRow
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
        public string? INTEGRITY_HASH { get; set; }
        public string? PREVIOUS_HASH { get; set; }
    }
}
