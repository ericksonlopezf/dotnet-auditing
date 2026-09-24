// Copyright © Erickson Lopez. MIT License.
using System;
using System.Net;
using System.Net.Sockets;

namespace EricksonLopez.Auditing;

/// <summary>Encapsulates technical execution context metadata for an audited action.</summary>
/// <param name="TenantId">The tenant identifier scoping the audit record.</param>
/// <param name="Source">The application or service component that generated the audit record.</param>
/// <param name="CorrelationId">The optional correlation identifier linking related operations across services.</param>
/// <param name="CausationId">The optional identifier of the direct causal event or command.</param>
/// <param name="RequestId">The optional transport-level request identifier.</param>
/// <param name="IpAddress">The optional network IP address of the originating client.</param>
/// <param name="UserAgent">The optional user agent string from the originating client request.</param>
/// <param name="IdempotencyKey">The optional idempotency key preventing duplicate writes during concurrent retries.</param>
/// <exception cref="ArgumentException"><paramref name="Source"/> is <see langword="null"/> or white-space</exception>
public sealed record AuditContext(
    TenantId TenantId,
    string Source,
    string? CorrelationId = null,
    string? CausationId = null,
    string? RequestId = null,
    string? IpAddress = null,
    string? UserAgent = null,
    string? IdempotencyKey = null)
{
    private readonly string _source = !string.IsNullOrWhiteSpace(Source) ? Source : throw new ArgumentException("Source cannot be null or whitespace.", nameof(Source));

    /// <summary>Gets the application or service component that generated the audit record.</summary>
    /// <exception cref="ArgumentException"><paramref name="value"/> is <see langword="null"/> or white-space</exception>
    public string Source
    {
        get => _source;
        init
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value);
            _source = value;
        }
    }

    /// <summary>Gets the reserved tenant identifier for system-level or platform-wide events.</summary>
    public static readonly TenantId SystemTenantId = new TenantId("system");

    /// <summary>
    /// Anonymizes the specified IP address for privacy compliance.
    /// </summary>
    /// <param name="ipAddress">The IP address string to anonymize.</param>
    /// <returns>The anonymized IP address string, or <see langword="null"/> if input is <see langword="null"/>, white-space, or invalid.</returns>
    public static string? AnonymizeIp(string? ipAddress)
    {
        if (string.IsNullOrWhiteSpace(ipAddress))
            return null;

        if (!IPAddress.TryParse(ipAddress, out var ip))
            return null;

        if (ip.AddressFamily == AddressFamily.InterNetwork)
        {
            var bytes = ip.GetAddressBytes();
            bytes[3] = 0;
            return new IPAddress(bytes).ToString();
        }

        if (ip.AddressFamily == AddressFamily.InterNetworkV6)
        {
            var bytes = ip.GetAddressBytes();
            for (int i = 6; i < 16; i++)
            {
                bytes[i] = 0;
            }
            return new IPAddress(bytes).ToString();
        }

        return null;
    }
}
