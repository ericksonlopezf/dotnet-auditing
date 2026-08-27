// Copyright © Erickson Lopez. MIT License.
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace EricksonLopez.Auditing;

/// <summary>
/// Filters change tracking records to suppress, redact, or hash sensitive field values according to configured policies.
/// </summary>
public sealed class AuditSensitivityPipeline
{
    private readonly AuditConfiguration _config;

    /// <summary>Initializes a new instance of the <see cref="AuditSensitivityPipeline"/> class.</summary>
    /// <param name="config">The audit configuration containing sensitivity policies.</param>
    /// <exception cref="ArgumentNullException"><paramref name="config"/> is <see langword="null"/></exception>
    public AuditSensitivityPipeline(AuditConfiguration config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
    }

    /// <summary>
    /// Applies sensitivity policies to a collection of field changes, returning a sanitized collection.
    /// </summary>
    /// <param name="changes">The raw collection of field changes to process.</param>
    /// <returns>A sanitized read-only list of changes, or <see langword="null"/> if all changes are excluded or the input was <see langword="null"/>.</returns>
    public IReadOnlyList<AuditChange>? Apply(IReadOnlyList<AuditChange>? changes)
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

    /// <summary>Calculates the lowercase hexadecimal SHA-256 digest of the specified string value.</summary>
    /// <param name="value">The plaintext string value to hash.</param>
    /// <returns>The lowercase hexadecimal SHA-256 digest of the input string.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is <see langword="null"/></exception>
    public static string HashValue(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
