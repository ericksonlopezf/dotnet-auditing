// Copyright © Erickson Lopez. MIT License.
using System;
using System.Collections.Generic;

namespace EricksonLopez.Auditing;

/// <summary>Represents the canonical, immutable evidence record of an audited event.</summary>
/// <remarks>
/// Audit records are immutable once persisted and capture the complete actor, action, resource, context, and outcome details.
/// </remarks>
public sealed record AuditRecord
{
    /// <summary>Gets the unique, time-ordered identifier of this record.</summary>
    public required Guid Id { get; init; }

    /// <summary>Gets the exact UTC timestamp when the action occurred.</summary>
    public required DateTimeOffset OccurredAt { get; init; }

    /// <summary>Gets the identity that performed the action.</summary>
    public required AuditActor Actor { get; init; }

    /// <summary>Gets the operation that was executed.</summary>
    public required AuditAction Action { get; init; }

    /// <summary>Gets the target resource on which the operation was performed.</summary>
    public required AuditResource Resource { get; init; }

    /// <summary>Gets the observable result of the operation.</summary>
    public required AuditOutcome Outcome { get; init; }

    /// <summary>Gets the technical execution context at the time of the action.</summary>
    public required AuditContext Context { get; init; }

    /// <summary>Gets the optional collection of field-level changes captured during the action.</summary>
    public IReadOnlyList<AuditChange>? Changes { get; init; }

    /// <summary>Gets the optional error category or code when the outcome is unsuccessful.</summary>
    public string? ErrorCode { get; init; }

    /// <summary>Gets the optional cryptographic HMAC hash computed for this record and chained to previous records.</summary>
    public string? IntegrityHash { get; init; }

    /// <summary>Gets the optional cryptographic hash of the immediately preceding record in the chain.</summary>
    public string? PreviousHash { get; init; }
}
