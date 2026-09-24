// Copyright © Erickson Lopez. MIT License.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace EricksonLopez.Auditing;

/// <summary>Provides computation and verification of HMAC-SHA256 integrity hashes for cryptographic chaining of audit records.</summary>
public sealed class HmacAuditIntegrityService
{
    private readonly IAuditIntegrityProvider _keyProvider;
    private readonly IAuditHashAlgorithm _hashAlgorithm;

    /// <summary>Initializes a new instance of the <see cref="HmacAuditIntegrityService"/> class.</summary>
    /// <param name="keyProvider">The cryptographic key provider for tenant integrity keys.</param>
    /// <param name="hashAlgorithm">The cryptographic hash algorithm used for integrity signatures.</param>
    /// <exception cref="ArgumentNullException"><paramref name="keyProvider"/> or <paramref name="hashAlgorithm"/> is <see langword="null"/></exception>
    public HmacAuditIntegrityService(IAuditIntegrityProvider keyProvider, IAuditHashAlgorithm hashAlgorithm)
    {
        _keyProvider = keyProvider ?? throw new ArgumentNullException(nameof(keyProvider));
        _hashAlgorithm = hashAlgorithm ?? throw new ArgumentNullException(nameof(hashAlgorithm));
    }

    /// <summary>Computes the HMAC-SHA256 hash for an audit record, incorporating the preceding record hash.</summary>
    /// <param name="record">The audit record to hash.</param>
    /// <param name="previousHash">The hash of the preceding record in the tenant chain, or <see langword="null"/> for the initial record.</param>
    /// <returns>The lowercase hexadecimal HMAC-SHA256 digest string.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="record"/> is <see langword="null"/></exception>
    public string ComputeHash(AuditRecord record, string? previousHash)
    {
        ArgumentNullException.ThrowIfNull(record);

        Span<char> initialCharSpan = stackalloc char[512];
        var charBuffer = new CharBuffer(initialCharSpan);
        try
        {
            BuildCanonicalChars(ref charBuffer, record, previousHash);
            var canonicalChars = charBuffer.AsSpan();

            int maxByteCount = Encoding.UTF8.GetMaxByteCount(canonicalChars.Length);
            byte[]? byteBuffer = maxByteCount > 1024
                ? System.Buffers.ArrayPool<byte>.Shared.Rent(maxByteCount)
                : null;
            Span<byte> utf8Span = byteBuffer != null
                ? byteBuffer.AsSpan(0, maxByteCount)
                : stackalloc byte[1024];

            try
            {
                int byteCount = Encoding.UTF8.GetBytes(canonicalChars, utf8Span);
                var canonicalBytes = utf8Span.Slice(0, byteCount);

                var key = _keyProvider.GetCurrentKey(record.Context.TenantId.Value);

                Span<byte> hash = stackalloc byte[_hashAlgorithm.HashLengthInBytes];
                _hashAlgorithm.ComputeHash(canonicalBytes, key.Span, hash);
#if NET9_0_OR_GREATER
                return Convert.ToHexStringLower(hash);
#else
                return string.Create(hash.Length * 2, hash.ToArray(), (chars, bytes) =>
                {
                    const string hexAlphabet = "0123456789abcdef";
                    for (int i = 0; i < bytes.Length; i++)
                    {
                        chars[i * 2] = hexAlphabet[bytes[i] >> 4];
                        chars[i * 2 + 1] = hexAlphabet[bytes[i] & 0xF];
                    }
                });
#endif
            }
            finally
            {
                if (byteBuffer is not null)
                {
                    System.Buffers.ArrayPool<byte>.Shared.Return(byteBuffer);
                }
            }
        }
        finally
        {
            charBuffer.Dispose();
        }
    }

    /// <summary>Determines whether the integrity hash of a stored audit record matches its recomputed value.</summary>
    /// <param name="record">The audit record whose integrity is to be verified.</param>
    /// <returns><see langword="true"/> if the integrity hash is valid and matches; otherwise, <see langword="false"/>.</returns>
    public bool Verify(AuditRecord record)
    {
        if (record.IntegrityHash is null) return false;

        var expected = ComputeHash(record, record.PreviousHash);
        Span<byte> expectedBytes = stackalloc byte[expected.Length];
        Span<byte> actualBytes = stackalloc byte[record.IntegrityHash.Length];
        int expCount = Encoding.ASCII.GetBytes(expected, expectedBytes);
        int actCount = Encoding.ASCII.GetBytes(record.IntegrityHash, actualBytes);

        if (expCount != actCount) return false;

        return CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes);
    }

    // Canonical byte representation — deterministic, includes all persisted forensic fields
    private static void BuildCanonicalChars(ref CharBuffer buffer, AuditRecord record, string? previousHash)
    {
        buffer.AppendField(record.Id);
        buffer.AppendField(record.OccurredAt.ToUnixTimeMilliseconds());
        buffer.AppendField(record.Context.TenantId.Value);
        buffer.AppendField(record.Context.Source);
        buffer.AppendField((int)record.Actor.Type);
        buffer.AppendField(record.Actor.Id);
        buffer.AppendField(record.Actor.DisplayName);
        buffer.AppendField(record.Action.Code);
        buffer.AppendField(record.Resource.Type);
        buffer.AppendField(record.Resource.Id);
        buffer.AppendField(record.Resource.AggregateType);
        buffer.AppendField(record.Resource.AggregateId);
        buffer.AppendField((int)record.Outcome);
        buffer.AppendField(record.ErrorCode);
        buffer.AppendField(record.Context.CorrelationId);
        buffer.AppendField(record.Context.CausationId);
        buffer.AppendField(record.Context.RequestId);
        buffer.AppendField(record.Context.IpAddress);
        buffer.AppendField(record.Context.UserAgent);
        buffer.AppendField(previousHash);

        if (record.Changes is { Count: > 0 })
        {
            var sorted = new List<AuditChange>(record.Changes);
            sorted.Sort((a, b) => string.CompareOrdinal(a.Field, b.Field));
            buffer.Append(sorted.Count);
            buffer.Append('#');
            for (int i = 0; i < sorted.Count; i++)
            {
                var c = sorted[i];
                buffer.AppendField(c.Field);
                buffer.AppendField(c.OldValue);
                buffer.AppendField(c.NewValue);
                buffer.Append(c.IsRedacted ? '1' : '0');
                buffer.Append('|');
            }
        }
        else
        {
            buffer.Append("0#");
        }
    }

    private ref struct CharBuffer
    {
        private char[]? _rentedBuffer;
        private Span<char> _buffer;
        private int _position;

        public CharBuffer(Span<char> initialBuffer)
        {
            _rentedBuffer = null;
            _buffer = initialBuffer;
            _position = 0;
        }

        public void Append(scoped ReadOnlySpan<char> value)
        {
            EnsureCapacity(value.Length);
            value.CopyTo(_buffer.Slice(_position));
            _position += value.Length;
        }

        public void Append(char value)
        {
            EnsureCapacity(1);
            _buffer[_position++] = value;
        }

        public void Append(int value)
        {
            Span<char> span = stackalloc char[11];
            value.TryFormat(span, out int charsWritten, default, CultureInfo.InvariantCulture);
            Append(span.Slice(0, charsWritten));
        }


        public void AppendField(string? value)
        {
            if (value is null)
            {
                Append("-1~");
            }
            else
            {
                Append(value.Length);
                Append('~');
                Append(value);
            }
            Append('|');
        }

        public void AppendField(int value)
        {
            Span<char> span = stackalloc char[11];
            value.TryFormat(span, out int charsWritten, default, CultureInfo.InvariantCulture);
            Append(charsWritten);
            Append('~');
            Append(span.Slice(0, charsWritten));
            Append('|');
        }

        public void AppendField(long value)
        {
            Span<char> span = stackalloc char[20];
            value.TryFormat(span, out int charsWritten, default, CultureInfo.InvariantCulture);
            Append(charsWritten);
            Append('~');
            Append(span.Slice(0, charsWritten));
            Append('|');
        }

        public void AppendField(Guid value)
        {
            Span<char> span = stackalloc char[32]; // "N" format is 32 chars
            value.TryFormat(span, out int charsWritten, "N");
            Append(charsWritten);
            Append('~');
            Append(span.Slice(0, charsWritten));
            Append('|');
        }

        private void EnsureCapacity(int length)
        {
            if (_position + length > _buffer.Length)
            {
                int newCap = Math.Max(_buffer.Length * 2, _position + length);
                var newBuffer = System.Buffers.ArrayPool<char>.Shared.Rent(newCap);
                _buffer.Slice(0, _position).CopyTo(newBuffer);
                if (_rentedBuffer != null)
                {
                    System.Buffers.ArrayPool<char>.Shared.Return(_rentedBuffer);
                }
                _rentedBuffer = newBuffer;
                _buffer = newBuffer;
            }
        }

        public ReadOnlySpan<char> AsSpan() => _buffer.Slice(0, _position);

        public void Dispose()
        {
            if (_rentedBuffer != null)
            {
                System.Buffers.ArrayPool<char>.Shared.Return(_rentedBuffer);
                _rentedBuffer = null;
            }
        }
    }
}

