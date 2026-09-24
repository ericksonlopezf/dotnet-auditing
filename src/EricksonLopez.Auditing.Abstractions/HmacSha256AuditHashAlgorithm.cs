// Copyright © Erickson Lopez. MIT License.
using System;
using System.Security.Cryptography;

namespace EricksonLopez.Auditing;

/// <summary>Provides an HMAC-SHA256 implementation of <see cref="IAuditHashAlgorithm"/> for audit record integrity.</summary>
public sealed class HmacSha256AuditHashAlgorithm : IAuditHashAlgorithm
{
    /// <inheritdoc />
    public int HashLengthInBytes => 32;

    /// <inheritdoc />
    public string AlgorithmId => "HMACSHA256";

    /// <inheritdoc />
    public int ComputeHash(ReadOnlySpan<byte> data, ReadOnlySpan<byte> key, Span<byte> destination)
    {
        return HMACSHA256.HashData(key, data, destination);
    }
}
