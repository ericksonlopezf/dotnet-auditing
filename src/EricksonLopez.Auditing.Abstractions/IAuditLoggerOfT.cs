// Copyright © Erickson Lopez. MIT License.

namespace EricksonLopez.Auditing;

/// <summary>
/// Provides a strongly-typed facade for logging audit records associated with a specific category type.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Major Code Smell", "S2326:Unused type parameters should be removed", Justification = "Marker generic interface representing logger category name conforming to standard Microsoft.Extensions.Logging pattern.")]
public interface IAuditLogger<out TCategoryName> : IAuditLogger
{
}
