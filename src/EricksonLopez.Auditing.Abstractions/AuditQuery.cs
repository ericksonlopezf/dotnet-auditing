// Copyright © Erickson Lopez. MIT License.
using System;

namespace EricksonLopez.Auditing;

/// <summary>Represents filtering and keyset pagination parameters for querying persisted audit records.</summary>
public sealed record AuditQuery
{
    /// <summary>Gets the tenant identifier scoping the query.</summary>
    public required string TenantId { get; init; }

    /// <summary>Gets the optional actor identifier filter.</summary>
    public string? ActorId { get; init; }

    /// <summary>Gets the optional action code filter.</summary>
    public string? ActionCode { get; init; }

    /// <summary>Gets the optional resource type filter.</summary>
    public string? ResourceType { get; init; }

    /// <summary>Gets the optional resource identifier filter.</summary>
    public string? ResourceId { get; init; }

    /// <summary>Gets the optional outcome status filter.</summary>
    public AuditOutcome? Outcome { get; init; }

    /// <summary>Gets the optional earliest occurrence timestamp filter, inclusive.</summary>
    public DateTimeOffset? From { get; init; }

    /// <summary>Gets the optional latest occurrence timestamp filter, inclusive.</summary>
    public DateTimeOffset? To { get; init; }

    /// <summary>Gets the optional correlation identifier filter.</summary>
    public string? CorrelationId { get; init; }

    /// <summary>
    /// Gets the optional keyset continuation cursor indicating the last record identifier from the previous page.
    /// </summary>
    public Guid? AfterRecordId { get; init; }

    /// <summary>Gets the maximum number of records to return in a single page.</summary>
    public int PageSize { get; init; } = 50;
}
