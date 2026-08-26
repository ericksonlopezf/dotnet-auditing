// Copyright © Erickson Lopez. MIT License.
using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace EricksonLopez.Auditing;

/// <summary>Computes and verifies HMAC-SHA256 integrity hashes for cryptographic chaining of audit records.</summary>
public sealed class HmacAuditIntegrityService
{
    private readonly IAuditIntegrityProvider _keyProvider;

    /// <summary>Initializes a new instance of the <see cref="HmacAuditIntegrityService"/> class.</summary>
    /// <param name="keyProvider">The cryptographic key provider for tenant integrity keys.</param>
    /// <exception cref="ArgumentNullException"><paramref name="keyProvider"/> is <see langword="null"/></exception>
    public HmacAuditIntegrityService(IAuditIntegrityProvider keyProvider)
    {
        _keyProvider = keyProvider ?? throw new ArgumentNullException(nameof(keyProvider));
    }

    /// <summary>Computes the HMAC-SHA256 hash for an audit record, incorporating the preceding record hash.</summary>
    /// <param name="record">The audit record to hash.</param>
    /// <param name="previousHash">The hash of the preceding record in the tenant chain, or <see langword="null"/> for the initial record.</param>
    /// <returns>The lowercase hexadecimal HMAC-SHA256 digest string.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="record"/> is <see langword="null"/></exception>
    public string ComputeHash(AuditRecord record, string? previousHash)
    {
        ArgumentNullException.ThrowIfNull(record);

        var canonical = BuildCanonicalBytes(record, previousHash);
        var key = _keyProvider.GetCurrentKey(record.Context.TenantId);

        using var hmac = new HMACSHA256(key.ToArray());
        var hash = hmac.ComputeHash(canonical);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>Determines whether the integrity hash of a stored audit record matches its recomputed value.</summary>
    /// <param name="record">The audit record whose integrity is to be verified.</param>
    /// <returns><see langword="true"/> if the integrity hash is valid and matches; otherwise, <see langword="false"/>.</returns>
    public bool Verify(AuditRecord record)
    {
        if (record.IntegrityHash is null) return false;

        var expected = ComputeHash(record, record.PreviousHash);
        return CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(expected),
            Encoding.ASCII.GetBytes(record.IntegrityHash));
    }

    // Canonical byte representation — deterministic, excludes mutable fields
    private static byte[] BuildCanonicalBytes(AuditRecord record, string? previousHash)
    {
        // Canonical form: pipe-delimited key fields + previous hash
        // Intentionally excludes IntegrityHash and PreviousHash (those are the output/input)
        var canonical = string.Concat(
            record.Id.ToString("N"),
            "|",
            record.OccurredAt.ToUnixTimeMilliseconds(),
            "|",
            record.Context.TenantId,
            "|",
            record.Actor.Type.ToString(),
            "|",
            record.Actor.Id,
            "|",
            record.Action.Code,
            "|",
            record.Resource.Type,
            "|",
            record.Resource.Id,
            "|",
            ((int)record.Outcome).ToString(CultureInfo.InvariantCulture),
            "|",
            previousHash ?? string.Empty);

        return Encoding.UTF8.GetBytes(canonical);
    }
}
