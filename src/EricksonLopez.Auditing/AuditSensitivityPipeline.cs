// Copyright © Erickson Lopez. MIT License.
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace EricksonLopez.Auditing;

/// <summary>
/// Provides a pipeline that filters change tracking records to suppress, redact, or hash sensitive field values according to configured policies.
/// </summary>
public sealed class AuditSensitivityPipeline : IAuditSensitivityPipeline
{
    private readonly AuditConfiguration _config;
    private readonly IAuditCryptoKeyProvider? _keyProvider;

    /// <summary>Initializes a new instance of the <see cref="AuditSensitivityPipeline"/> class.</summary>
    /// <param name="config">The audit configuration containing sensitivity policies.</param>
    /// <param name="keyProvider">The optional crypto key provider for GDPR crypto-shredding.</param>
    /// <exception cref="ArgumentNullException"><paramref name="config"/> is <see langword="null"/></exception>
    public AuditSensitivityPipeline(AuditConfiguration config, IAuditCryptoKeyProvider? keyProvider = null)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _keyProvider = keyProvider;
    }

    /// <summary>Gets the optional crypto key provider configured for GDPR crypto-shredding.</summary>
    public IAuditCryptoKeyProvider? KeyProvider => _keyProvider;

    /// <inheritdoc/>
    public async ValueTask<AuditRecord> SanitizeAsync(AuditRecord record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);

        var sanitizedChanges = await ApplyAsync(record.Changes, record.Context.TenantId, cancellationToken).ConfigureAwait(false);
        if (ReferenceEquals(sanitizedChanges, record.Changes))
        {
            return record;
        }

        return record with { Changes = sanitizedChanges };
    }

    /// <inheritdoc/>
    /// <remarks>
    /// The <paramref name="tenantId"/> parameter is accepted for API compatibility with <see cref="IAuditSensitivityPipeline"/>
    /// and is reserved for future per-tenant crypto-shredding key lookups via <see cref="IAuditCryptoKeyProvider"/>.
    /// In the current implementation, it is not consumed — denylist and redaction policies apply globally across tenants.
    /// </remarks>
    public async ValueTask<IReadOnlyList<AuditChange>?> ApplyAsync(IReadOnlyList<AuditChange>? changes, string tenantId, CancellationToken cancellationToken = default)
    {
        if (changes is null || changes.Count == 0)
        {
            return changes;
        }

        List<AuditChange>? result = null;

        for (int i = 0; i < changes.Count; i++)
        {
            var change = changes[i];

            if (_config.GlobalFieldDenylist.Contains(change.Field))
            {
                result ??= InitializeResult(changes, i);
                continue;
            }

            if (change.IsRedacted)
            {
                result ??= InitializeResult(changes, i);
                result.Add(AuditChange.Redacted(change.Field));
                continue;
            }

            // Truncate strings that are too long
            var truncated = false;
            var newOld = change.OldValue;
            var newNew = change.NewValue;

            if (newOld?.Length > _config.MaxStringLength)
            {
                newOld = string.Concat(newOld.AsSpan(0, _config.MaxStringLength), "[TRUNCATED]");
                truncated = true;
            }

            if (newNew?.Length > _config.MaxStringLength)
            {
                newNew = string.Concat(newNew.AsSpan(0, _config.MaxStringLength), "[TRUNCATED]");
                truncated = true;
            }

            if (truncated)
            {
                result ??= InitializeResult(changes, i);
                result.Add(new AuditChange(change.Field, newOld, newNew, change.IsRedacted));
                continue;
            }

            result?.Add(change);
        }

        if (result is null)
        {
            return changes;
        }

        return result.Count == 0 ? null : result;
    }

    private static List<AuditChange> InitializeResult(IReadOnlyList<AuditChange> changes, int upToIndex)
    {
        var list = new List<AuditChange>(changes.Count);
        for (int j = 0; j < upToIndex; j++)
        {
            list.Add(changes[j]);
        }
        return list;
    }

    /// <summary>Calculates the lowercase hexadecimal SHA-256 digest of the specified string value, with optional cryptographic salt.</summary>
    /// <param name="value">The plaintext string value to hash.</param>
    /// <param name="salt">The optional salt to prevent rainbow table attacks on low-entropy values.</param>
    /// <returns>The lowercase hexadecimal SHA-256 digest of the input string.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is <see langword="null"/></exception>
    public static string HashValue(string value, string? salt = null)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (string.IsNullOrEmpty(salt))
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
            return Convert.ToHexString(bytes).ToLowerInvariant();
        }

        var combined = $"{salt}:{value}";
        var saltedBytes = SHA256.HashData(Encoding.UTF8.GetBytes(combined));
        return Convert.ToHexString(saltedBytes).ToLowerInvariant();
    }
}
