// Copyright © Erickson Lopez. MIT License.
using System;
using System.Collections.Generic;

namespace EricksonLopez.Auditing;

/// <summary>Represents the paginated results of an audit record query operation.</summary>
/// <param name="Records">The collection of matching audit records returned for this page.</param>
/// <param name="NextPageToken">The continuation token to supply as <see cref="AuditQuery.ContinuationToken"/> for the next page, or <see langword="null"/> if on the final page.</param>
/// <param name="HasMore">A value indicating whether additional matching records exist beyond the current page.</param>
public sealed record AuditQueryResult(
    IReadOnlyList<AuditRecord> Records,
    string? NextPageToken,
    bool HasMore);
