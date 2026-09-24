// Copyright © Erickson Lopez. MIT License.
using System.Threading;
using System.Threading.Tasks;

namespace EricksonLopez.Auditing;

/// <summary>
/// Provides cryptographic key management for the Crypto-Shredding pattern (GDPR Art. 17).
/// </summary>
public interface IAuditCryptoKeyProvider
{
    /// <summary>
    /// Retrieves or generates the encryption key associated with a specific subject (e.g., tenant or actor).
    /// </summary>
    /// <param name="subjectId">The identifier of the subject whose data requires encryption.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous operation. The task result contains a 32-byte cryptographic key for AES-GCM encryption.</returns>
    ValueTask<byte[]> GetEncryptionKeyAsync(string subjectId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Permanently deletes the encryption key associated with a specific subject, rendering all their encrypted PII permanently inaccessible.
    /// </summary>
    /// <param name="subjectId">The identifier of the subject whose key should be destroyed.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    ValueTask ShredKeyAsync(string subjectId, CancellationToken cancellationToken = default);
}
