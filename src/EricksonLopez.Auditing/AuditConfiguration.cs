// Copyright © Erickson Lopez. MIT License.
using System;
using System.Collections.Generic;

namespace EricksonLopez.Auditing;

/// <summary>Represents runtime configuration options for the auditing pipeline.</summary>
public sealed class AuditConfiguration
{
    /// <summary>
    /// Gets or sets the default failure handling behavior when the audit store fails.
    /// </summary>
    /// <remarks>Defaults to <see cref="AuditFailureBehavior.FailClosed"/>.</remarks>
    public AuditFailureBehavior DefaultFailureBehavior { get; set; } = AuditFailureBehavior.FailClosed;

    /// <summary>
    /// Gets the set of action codes for which store failures must always propagate regardless of <see cref="DefaultFailureBehavior"/>.
    /// </summary>
    public HashSet<string> CriticalActionCodes { get; } = new(StringComparer.OrdinalIgnoreCase)
    {
        AuditAction.Login.Code,
        AuditAction.Delete.Code,
        AuditAction.GrantPermission.Code,
        AuditAction.RevokePermission.Code
    };

    /// <summary>
    /// Gets the global set of field names always excluded from change tracking records across all types.
    /// </summary>
    public HashSet<string> GlobalFieldDenylist { get; } = new(StringComparer.OrdinalIgnoreCase)
    {
        "Password",
        "PasswordHash",
        "PasswordSalt",
        "Token",
        "AccessToken",
        "RefreshToken",
        "Secret",
        "ApiKey",
        "ApiSecret",
        "ClientSecret",
        "PrivateKey",
        "Certificate",
        "CreditCardNumber",
        "Cvv",
        "Ssn",
        "Pin",
        "SecurityAnswer"
    };

    /// <summary>
    /// Gets or sets a value indicating whether HMAC chain integrity verification is enabled globally.
    /// </summary>
    public bool EnableIntegrityChain { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of records buffered in the batch channel before applying backpressure.
    /// </summary>
    public int BatchChannelCapacity { get; set; } = 1000;

    /// <summary>Gets or sets the maximum number of records persisted per batch flush operation.</summary>
    public int BatchSize { get; set; } = 100;

    /// <summary>Gets or sets the maximum duration to wait before flushing a partially filled batch.</summary>
    public TimeSpan BatchFlushInterval { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Gets or sets the maximum allowed string length for change values.
    /// </summary>
    /// <remarks>
    /// Strings exceeding this length will be truncated to prevent Large Object Heap (LOH) allocations and database exceptions.
    /// Defaults to 4000 characters. Values are clamped to a maximum limit of 8192 characters.
    /// </remarks>
    public int MaxStringLength
    {
        get => _maxStringLength;
        set => _maxStringLength = Math.Min(value, 8192);
    }

    private int _maxStringLength = 4000;
}
