// Copyright © Erickson Lopez. MIT License.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Polly;
using Polly.Retry;

namespace EricksonLopez.Auditing;

/// <summary>
/// Provides cryptographic HMAC-SHA256 integrity chaining for an <see cref="IAuditStore"/>
/// before records are persisted to the underlying store.
/// </summary>
public sealed class IntegrityAuditStoreDecorator : IAuditStore
{
    private readonly IAuditStore _innerStore;
    private readonly HmacAuditIntegrityService _integrityService;

    private static readonly ResiliencePipeline _retryPipeline = new ResiliencePipelineBuilder()
        .AddRetry(new RetryStrategyOptions
        {
            // Retry on any exception that might be caused by a unique constraint violation on PreviousHash
            ShouldHandle = new PredicateBuilder().Handle<Exception>(),
            Delay = TimeSpan.FromMilliseconds(50),
            MaxRetryAttempts = 5,
            BackoffType = DelayBackoffType.Exponential,
            UseJitter = true
        })
        .Build();

    /// <summary>
    /// Initializes a new instance of the <see cref="IntegrityAuditStoreDecorator"/> class.
    /// </summary>
    /// <param name="innerStore">The underlying audit store where records are persisted.</param>
    /// <param name="integrityService">The service used to compute HMAC integrity hashes.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="innerStore"/> or <paramref name="integrityService"/> is <see langword="null"/>
    /// </exception>
    public IntegrityAuditStoreDecorator(
        IAuditStore innerStore,
        HmacAuditIntegrityService integrityService)
    {
        _innerStore = innerStore ?? throw new ArgumentNullException(nameof(innerStore));
        _integrityService = integrityService ?? throw new ArgumentNullException(nameof(integrityService));
    }

    /// <inheritdoc/>
    public async ValueTask AppendAsync(AuditRecord record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);

        await _retryPipeline.ExecuteAsync(async ct =>
        {
            // 1. Get the latest record for this tenant to fetch its IntegrityHash
            var query = new AuditQuery { TenantId = record.Context.TenantId, PageSize = 1 };
            var queryResult = await _innerStore.QueryAsync(query, ct).ConfigureAwait(false);

            var previousHash = queryResult.Records.Count > 0 ? queryResult.Records[0].IntegrityHash : null;

            // 2. Compute the new hash
            var integrityHash = _integrityService.ComputeHash(record, previousHash);

            // 3. Clone the immutable record with the hashes
            var secureRecord = record with
            {
                PreviousHash = previousHash,
                IntegrityHash = integrityHash
            };

            // 4. Persist
            await _innerStore.AppendAsync(secureRecord, ct).ConfigureAwait(false);

        }, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask AppendBatchAsync(IReadOnlyList<AuditRecord> records, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(records);
        if (records.Count == 0) return;

        // Group by tenant to ensure we chain correctly per tenant
        var recordsByTenant = records.GroupBy(r => r.Context.TenantId.Value).ToList();

        foreach (var tenantGroup in recordsByTenant)
        {
            await _retryPipeline.ExecuteAsync(async ct =>
            {
                var tenantIdString = tenantGroup.Key;
                var tenantId = new TenantId(tenantIdString);

                var query = new AuditQuery { TenantId = tenantId, PageSize = 1 };
                var queryResult = await _innerStore.QueryAsync(query, ct).ConfigureAwait(false);

                var previousHash = queryResult.Records.Count > 0 ? queryResult.Records[0].IntegrityHash : null;
                var secureRecords = new List<AuditRecord>(tenantGroup.Count());

                foreach (var record in tenantGroup)
                {
                    var integrityHash = _integrityService.ComputeHash(record, previousHash);

                    var secureRecord = record with
                    {
                        PreviousHash = previousHash,
                        IntegrityHash = integrityHash
                    };

                    secureRecords.Add(secureRecord);
                    previousHash = integrityHash; // Chain locally within the batch!
                }

                await _innerStore.AppendBatchAsync(secureRecords, ct).ConfigureAwait(false);

            }, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    public ValueTask<AuditQueryResult> QueryAsync(AuditQuery query, CancellationToken cancellationToken = default)
    {
        return _innerStore.QueryAsync(query, cancellationToken);
    }
}
