// Copyright © Erickson Lopez. MIT License.
using System;

namespace EricksonLopez.Auditing.EntityFrameworkCore;

/// <summary>Represents a persisted audit record entity in an Entity Framework Core data store.</summary>
public sealed class AuditRecordEntity
{
    /// <summary>Gets or sets the unique, time-ordered identifier of the audit record.</summary>
    public Guid Id { get; set; }

    /// <summary>Gets or sets the timestamp when the action occurred in UTC.</summary>
    public DateTimeOffset OccurredAt { get; set; }

    /// <summary>Gets or sets the tenant identifier scoping the audit record.</summary>
    public string TenantId { get; set; } = null!;

    /// <summary>Gets or sets the originating system source component.</summary>
    public string Source { get; set; } = null!;

    /// <summary>Gets or sets the classification code of the actor.</summary>
    public byte ActorType { get; set; }

    /// <summary>Gets or sets the stable identifier of the actor.</summary>
    public string ActorId { get; set; } = null!;

    /// <summary>Gets or sets the optional display name of the actor.</summary>
    public string? ActorName { get; set; }

    /// <summary>Gets or sets the action code identifying the executed operation.</summary>
    public string ActionCode { get; set; } = null!;

    /// <summary>Gets or sets the logical type of the target resource.</summary>
    public string ResourceType { get; set; } = null!;

    /// <summary>Gets or sets the stable identifier of the target resource.</summary>
    public string ResourceId { get; set; } = null!;

    /// <summary>Gets or sets the optional aggregate root entity type.</summary>
    public string? AggregateType { get; set; }

    /// <summary>Gets or sets the optional aggregate root identifier.</summary>
    public string? AggregateId { get; set; }

    /// <summary>Gets or sets the outcome classification code of the operation.</summary>
    public byte Outcome { get; set; }

    /// <summary>Gets or sets the optional error code for unsuccessful outcomes.</summary>
    public string? ErrorCode { get; set; }

    /// <summary>Gets or sets the optional correlation identifier linking related operations.</summary>
    public string? CorrelationId { get; set; }

    /// <summary>Gets or sets the optional identifier of the direct causal event.</summary>
    public string? CausationId { get; set; }

    /// <summary>Gets or sets the optional transport-level request identifier.</summary>
    public string? RequestId { get; set; }

    /// <summary>Gets or sets the optional client network IP address.</summary>
    public string? IpAddress { get; set; }

    /// <summary>Gets or sets the optional client user agent string.</summary>
    public string? UserAgent { get; set; }

    /// <summary>Gets or sets the serialized JSON payload representing field-level changes.</summary>
    public string? ChangesJson { get; set; }

    /// <summary>Gets or sets the cryptographic HMAC hash for chain integrity verification.</summary>
    public string? IntegrityHash { get; set; }

    /// <summary>Gets or sets the cryptographic HMAC hash of the preceding record in the chain.</summary>
    public string? PreviousHash { get; set; }
}
