// Copyright © Erickson Lopez. MIT License.

namespace EricksonLopez.Auditing;

/// <summary>Identifies the target resource on which an audited action was performed.</summary>
/// <param name="Type">The logical type or entity class of the resource.</param>
/// <param name="Id">The stable identifier of the specific resource instance.</param>
/// <param name="AggregateType">The optional aggregate root entity type when the resource is a child entity.</param>
/// <param name="AggregateId">The optional identifier of the aggregate root entity.</param>
public sealed record AuditResource(
    string Type,
    string Id,
    string? AggregateType = null,
    string? AggregateId = null);
