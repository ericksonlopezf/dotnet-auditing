// Copyright © Erickson Lopez. MIT License.
using System;

namespace EricksonLopez.Auditing;

/// <summary>Defines a cryptographic hashing algorithm used to compute the integrity signature of an audit record.</summary>
public interface IAuditHashAlgorithm
{
    /// <summary>Computes the cryptographic hash for the given data and key, writing the result into the destination span.</summary>
    /// <param name="data">The canonicalized payload data to hash.</param>
    /// <param name="key">The cryptographic key.</param>
    /// <param name="destination">The destination span to receive the computed hash bytes.</param>
    /// <returns>The number of bytes written to the destination span.</returns>
    int ComputeHash(ReadOnlySpan<byte> data, ReadOnlySpan<byte> key, Span<byte> destination);

    /// <summary>Gets the exact length in bytes of the hash produced by this algorithm.</summary>
    int HashLengthInBytes { get; }

    /// <summary>Gets the string identifier of this algorithm, such as "HMACSHA256".</summary>
    string AlgorithmId { get; }
}
