// Copyright © Erickson Lopez. MIT License.

namespace EricksonLopez.Auditing;

/// <summary>Represents a single field-level state change captured during an audited action.</summary>
/// <param name="Field">The name of the modified property or field.</param>
/// <param name="OldValue">The value before the change, or <see langword="null"/> for newly created resources.</param>
/// <param name="NewValue">The value after the change, or <see langword="null"/> for deleted resources.</param>
/// <param name="IsRedacted">A value indicating whether the actual values were withheld by sensitivity policies.</param>
public sealed record AuditChange(
    string Field,
    string? OldValue,
    string? NewValue,
    bool IsRedacted = false)
{
    /// <summary>Creates a redacted change entry for a sensitive field with suppressed values.</summary>
    /// <param name="field">The name of the sensitive field that was modified.</param>
    /// <returns>A new <see cref="AuditChange"/> instance with <see cref="AuditChange.IsRedacted"/> set to <see langword="true"/>.</returns>
    public static AuditChange Redacted(string field) => new(field, null, null, IsRedacted: true);
}
