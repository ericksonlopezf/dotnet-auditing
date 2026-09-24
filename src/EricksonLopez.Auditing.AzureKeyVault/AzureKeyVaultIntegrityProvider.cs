// Copyright © Erickson Lopez. MIT License.
using System;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using EricksonLopez.Auditing;

namespace EricksonLopez.Auditing.AzureKeyVault;

/// <summary>Provides cryptographic integrity keys fetched dynamically from Azure Key Vault.</summary>
public sealed class AzureKeyVaultIntegrityProvider : IAuditIntegrityProvider
{
    private readonly SecretClient _secretClient;
    private readonly ConcurrentDictionary<string, byte[]> _keyCache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Initializes a new instance of the <see cref="AzureKeyVaultIntegrityProvider"/> class.</summary>
    /// <param name="keyVaultUri">The URI to the Azure Key Vault.</param>
    /// <exception cref="ArgumentNullException"><paramref name="keyVaultUri"/> is <see langword="null"/></exception>
    public AzureKeyVaultIntegrityProvider(Uri keyVaultUri)
    {
        ArgumentNullException.ThrowIfNull(keyVaultUri);
        _secretClient = new SecretClient(keyVaultUri, new DefaultAzureCredential());
    }

    /// <summary>Initializes a new instance of the <see cref="AzureKeyVaultIntegrityProvider"/> class with a custom <see cref="SecretClient"/>.</summary>
    /// <param name="secretClient">The secret client.</param>
    /// <exception cref="ArgumentNullException"><paramref name="secretClient"/> is <see langword="null"/></exception>
    public AzureKeyVaultIntegrityProvider(SecretClient secretClient)
    {
        ArgumentNullException.ThrowIfNull(secretClient);
        _secretClient = secretClient;
    }

    /// <inheritdoc/>
    public ReadOnlyMemory<byte> GetCurrentKey(TenantId tenantId)
    {
        if (_keyCache.TryGetValue(tenantId.Value, out var cachedKey))
        {
            return cachedKey;
        }

        // Warning: This is synchronous-over-async and meant for demonstration. 
        // A production implementation would pre-fetch or use IAuditIntegrityProviderAsync.
        var secretName = $"audit-key-{tenantId.Value}";
        var secret = _secretClient.GetSecret(secretName).Value;

        var keyBytes = Convert.FromBase64String(secret.Value);
        _keyCache.TryAdd(tenantId.Value, keyBytes);

        return keyBytes;
    }
}
