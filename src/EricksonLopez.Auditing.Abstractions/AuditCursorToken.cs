// Copyright © Erickson Lopez. MIT License.
using System;
using System.Text;

namespace EricksonLopez.Auditing;

/// <summary>
/// Provides methods for encoding and decoding composite pagination cursor tokens.
/// </summary>
public static class AuditCursorToken
{
    /// <summary>
    /// Creates a base64-encoded continuation token containing the occurred_at timestamp and unique identifier.
    /// </summary>
    /// <param name="occurredAt">The occurrence timestamp of the record.</param>
    /// <param name="id">The unique identifier of the record.</param>
    /// <returns>A base64-encoded continuation token string.</returns>
    public static string Create(DateTimeOffset occurredAt, Guid id)
    {
        var unixMilliseconds = occurredAt.ToUnixTimeMilliseconds();
        var raw = $"{unixMilliseconds}:{id:N}";
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(raw));
    }

    /// <summary>
    /// Attempts to parse a base64-encoded continuation token into its timestamp and identifier components.
    /// </summary>
    /// <param name="token">The continuation token to parse.</param>
    /// <param name="occurredAt">When this method returns, contains the parsed occurrence timestamp if parsing succeeded; otherwise, the default value.</param>
    /// <param name="id">When this method returns, contains the parsed record identifier if parsing succeeded; otherwise, an empty identifier.</param>
    /// <returns><see langword="true"/> if the token was parsed successfully; otherwise, <see langword="false"/>.</returns>
    public static bool TryParse(string? token, out DateTimeOffset occurredAt, out Guid id)
    {
        occurredAt = default;
        id = Guid.Empty;

        if (string.IsNullOrWhiteSpace(token))
            return false;

        try
        {
            var rawBytes = Convert.FromBase64String(token);
            var rawStr = Encoding.UTF8.GetString(rawBytes);

            var parts = rawStr.Split(':');
            if (parts.Length != 2)
                return false;

            if (!long.TryParse(parts[0], out var unixMs))
                return false;

            if (!Guid.TryParse(parts[1], out id))
                return false;

            occurredAt = DateTimeOffset.FromUnixTimeMilliseconds(unixMs);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
