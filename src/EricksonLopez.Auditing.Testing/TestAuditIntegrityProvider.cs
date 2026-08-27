// Copyright © Erickson Lopez. MIT License.
using System;
using System.Collections.Concurrent;

namespace EricksonLopez.Auditing.Testing;

/// <summary>Provides an in-memory, configurable cryptographic key provider for testing HMAC integrity verification.</summary>
public sealed class TestAuditIntegrityProvider : IAuditIntegrityProvider
{
    /// <summary>Gets the default 256-bit symmetric test key bytes.</summary>
    public static readonly byte[] DefaultKey = new byte[32]
    {
        1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16,
        17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32
    };

    private readonly byte[] _defaultKey;
    private readonly ConcurrentDictionary<string, byte[]> _tenantKeys = new(StringComparer.Ordinal);

    /// <summary>Initializes a new instance of the <see cref="TestAuditIntegrityProvider"/> class with the default key.</summary>
    public TestAuditIntegrityProvider() : this(DefaultKey) { }

    /// <summary>Initializes a new instance of the <see cref="TestAuditIntegrityProvider"/> class with a custom default key.</summary>
    /// <param name="defaultKey">The default key byte array.</param>
    /// <exception cref="ArgumentNullException"><paramref name="defaultKey"/> is <see langword="null"/></exception>
    public TestAuditIntegrityProvider(byte[] defaultKey)
    {
        _defaultKey = defaultKey ?? throw new ArgumentNullException(nameof(defaultKey));
    }

    /// <summary>Configures a specific cryptographic key for the specified tenant.</summary>
    /// <param name="tenantId">The tenant identifier to configure.</param>
    /// <param name="key">The cryptographic key byte array.</param>
    /// <returns>The current <see cref="TestAuditIntegrityProvider"/> instance for method chaining.</returns>
    /// <exception cref="ArgumentException"><paramref name="tenantId"/> is <see langword="null"/> or empty</exception>
    /// <exception cref="ArgumentNullException"><paramref name="key"/> is <see langword="null"/></exception>
    public TestAuditIntegrityProvider SetTenantKey(string tenantId, byte[] key)
    {
        ArgumentException.ThrowIfNullOrEmpty(tenantId);
        ArgumentNullException.ThrowIfNull(key);
        _tenantKeys[tenantId] = key;
        return this;
    }

    /// <inheritdoc/>
    public ReadOnlyMemory<byte> GetCurrentKey(string tenantId)
    {
        if (!string.IsNullOrEmpty(tenantId) && _tenantKeys.TryGetValue(tenantId, out var key))
        {
            return key;
        }

        return _defaultKey;
    }
}
