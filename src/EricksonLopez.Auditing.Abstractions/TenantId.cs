// Copyright © Erickson Lopez. MIT License.
using System;
using System.Text.Json.Serialization;

namespace EricksonLopez.Auditing;

/// <summary>Represents a strongly-typed, immutable tenant identifier for auditing boundaries.</summary>
public readonly record struct TenantId
{
    /// <summary>Gets the raw string value of the tenant identifier.</summary>
    public string Value { get; }

    /// <summary>Initializes a new instance of the <see cref="TenantId"/> struct.</summary>
    /// <param name="value">The non-empty string value of the tenant.</param>
    /// <exception cref="ArgumentException"><paramref name="value"/> is <see langword="null"/> or white-space</exception>
    [JsonConstructor]
    public TenantId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    /// <inheritdoc/>
    public override string ToString() => Value;

    /// <summary>
    /// Converts a <see cref="TenantId"/> to a <see cref="string"/>.
    /// </summary>
    /// <param name="tenantId">The tenant identifier to convert.</param>
    /// <returns>The string representation of the tenant identifier.</returns>
    public static implicit operator string(TenantId tenantId) => tenantId.Value;

    /// <summary>
    /// Converts a <see cref="string"/> to a <see cref="TenantId"/>.
    /// </summary>
    /// <param name="value">The string value to convert.</param>
    /// <returns>A new <see cref="TenantId"/> instance initialized with the specified value.</returns>
    public static implicit operator TenantId(string value) => new(value);
}
