// Copyright © Erickson Lopez. MIT License.

namespace EricksonLopez.Auditing;

/// <summary>Captures technical execution context metadata for an audited action.</summary>
/// <param name="TenantId">The tenant identifier scoping the audit record.</param>
/// <param name="Source">The application or service component that generated the audit record.</param>
/// <param name="CorrelationId">The optional correlation identifier linking related operations across services.</param>
/// <param name="CausationId">The optional identifier of the direct causal event or command.</param>
/// <param name="RequestId">The optional transport-level request identifier.</param>
/// <param name="IpAddress">The optional network IP address of the originating client.</param>
/// <param name="UserAgent">The optional user agent string from the originating client request.</param>
public sealed record AuditContext(
    string TenantId,
    string Source,
    string? CorrelationId = null,
    string? CausationId = null,
    string? RequestId = null,
    string? IpAddress = null,
    string? UserAgent = null)
{
    /// <summary>Gets the reserved tenant identifier for system-level or platform-wide events.</summary>
    public const string SystemTenantId = "system";
}
