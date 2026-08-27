// Copyright © Erickson Lopez. MIT License.
using System;
using System.Threading;
using System.Threading.Tasks;

namespace EricksonLopez.Auditing;

/// <summary>Defines a verifier for evaluating cryptographic chain integrity across audit records.</summary>
public interface IAuditIntegrityVerifier
{
    /// <summary>Verifies the cryptographic HMAC chain for audit records within the specified time window.</summary>
    /// <param name="tenantId">The tenant identifier whose audit chain is to be verified.</param>
    /// <param name="from">The inclusive start timestamp of the verification window.</param>
    /// <param name="until">The inclusive end timestamp of the verification window.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the asynchronous operation.</param>
    /// <returns>A value task representing the asynchronous operation. The task result contains the verification outcome details.</returns>
    ValueTask<AuditIntegrityVerificationResult> VerifyChainAsync(
        string tenantId,
        DateTimeOffset from,
        DateTimeOffset until,
        CancellationToken cancellationToken = default);
}
