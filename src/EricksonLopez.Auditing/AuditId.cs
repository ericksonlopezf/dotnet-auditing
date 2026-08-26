// Copyright © Erickson Lopez. MIT License.
using System;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace EricksonLopez.Auditing;

/// <summary>Provides factory methods for generating time-ordered unique identifiers.</summary>
public static class AuditId
{
    /// <summary>Creates a new time-ordered unique identifier conforming to UUIDv7.</summary>
    /// <remarks>
    /// Embeds a millisecond-precision Unix timestamp in the most significant bits,
    /// ensuring monotonic chronological ordering within a partition when used as a primary key.
    /// </remarks>
    /// <returns>A new time-ordered <see cref="Guid"/> identifier.</returns>
    public static Guid NewId()
    {
#if NET9_0_OR_GREATER
        return Guid.CreateVersion7();
#else
        return CreateVersion7Compat();
#endif
    }

#if !NET9_0_OR_GREATER
    // UUIDv7 compatible construction for .NET 8
    // Layout: 48-bit Unix ms | 4-bit version (0111) | 12-bit rand_a | 2-bit variant (10) | 62-bit rand_b
    private static Guid CreateVersion7Compat()
    {
        Span<byte> bytes = stackalloc byte[16];
        RandomNumberGenerator.Fill(bytes);

        // Embed Unix time in milliseconds in the first 6 bytes (48 bits)
        var ms = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        bytes[0] = (byte)(ms >> 40);
        bytes[1] = (byte)(ms >> 32);
        bytes[2] = (byte)(ms >> 24);
        bytes[3] = (byte)(ms >> 16);
        bytes[4] = (byte)(ms >> 8);
        bytes[5] = (byte)ms;

        // Set version = 7 (0111) in bits 76-79 (byte 6, high nibble)
        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x70);

        // Set variant = 10 in bits 64-65 (byte 8, high bits)
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);

        return new Guid(bytes, bigEndian: true);
    }
#endif
}
