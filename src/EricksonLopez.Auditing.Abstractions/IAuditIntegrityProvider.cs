// Copyright © Erickson Lopez. MIT License.
using System;

namespace EricksonLopez.Auditing;

/// <summary>Defines a provider for retrieving cryptographic keys used in HMAC chain integrity verification.</summary>
public interface IAuditIntegrityProvider
{
    /// <summary>Retrieves the current cryptographic key bytes for the specified tenant.</summary>
    /// <param name="tenantId">The tenant identifier for which to retrieve the key.</param>
    /// <returns>A read-only memory segment containing the cryptographic key bytes.</returns>
    ReadOnlyMemory<byte> GetCurrentKey(string tenantId);
}
