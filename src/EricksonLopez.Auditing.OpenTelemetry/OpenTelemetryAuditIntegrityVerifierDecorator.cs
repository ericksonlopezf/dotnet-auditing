// Copyright © Erickson Lopez. MIT License.
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace EricksonLopez.Auditing.OpenTelemetry;

/// <summary>
/// Provides OpenTelemetry metrics and tracing instrumentation for an underlying <see cref="IAuditIntegrityVerifier"/>.
/// </summary>
public sealed class OpenTelemetryAuditIntegrityVerifierDecorator : IAuditIntegrityVerifier
{
    private readonly IAuditIntegrityVerifier _innerVerifier;

    /// <summary>Initializes a new instance of the <see cref="OpenTelemetryAuditIntegrityVerifierDecorator"/> class.</summary>
    /// <param name="innerVerifier">The underlying verifier to decorate.</param>
    /// <exception cref="ArgumentNullException"><paramref name="innerVerifier"/> is <see langword="null"/></exception>
    public OpenTelemetryAuditIntegrityVerifierDecorator(IAuditIntegrityVerifier innerVerifier)
    {
        _innerVerifier = innerVerifier ?? throw new ArgumentNullException(nameof(innerVerifier));
    }

    /// <inheritdoc/>
    public async ValueTask<AuditIntegrityVerificationResult> VerifyChainAsync(
        string tenantId,
        DateTimeOffset from,
        DateTimeOffset until,
        CancellationToken cancellationToken = default)
    {
        using var activity = AuditActivitySource.Source.StartActivity("Audit.VerifyChain");
        if (activity is not null)
        {
            activity.SetTag(AuditActivitySource.Tags.TenantId, tenantId);
        }

        var result = await _innerVerifier.VerifyChainAsync(tenantId, from, until, cancellationToken).ConfigureAwait(false);

        AuditMetrics.IntegrityVerifications.Add(1,
            new KeyValuePair<string, object?>(AuditActivitySource.Tags.TenantId, tenantId),
            new KeyValuePair<string, object?>("audit.verification_valid", result.IsValid));

        return result;
    }
}
