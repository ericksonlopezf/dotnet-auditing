// Copyright © Erickson Lopez. MIT License.

namespace EricksonLopez.Auditing;

/// <summary>Specifies how sensitive field values are handled in change tracking records.</summary>
/// <remarks>
/// <para>
/// <see cref="AuditSensitivityPipeline.Apply"/> automatically enforces the following policies:
/// </para>
/// <list type="bullet">
/// <item><description>
///   Fields whose names match any entry in <see cref="AuditConfiguration.GlobalFieldDenylist"/> (case-insensitive)
///   are completely excluded from the change list — equivalent to <see cref="Exclude"/>.
/// </description></item>
/// <item><description>
///   <see cref="AuditChange"/> entries with <see cref="AuditChange.IsRedacted"/> set to <see langword="true"/>
///   (or created via <see cref="AuditChange.Redacted"/>) have their values suppressed — equivalent to <see cref="Redact"/>.
/// </description></item>
/// </list>
/// <para>
///   <see cref="Hash"/> is NOT automatically applied by <see cref="AuditSensitivityPipeline.Apply"/>.
///   To store a one-way hash of a sensitive value, the caller must explicitly call
///   <see cref="AuditSensitivityPipeline.HashValue"/> and pass the result as the field value.
/// </para>
/// </remarks>
public enum AuditFieldSensitivity
{
    /// <summary>Records the field value without modification.</summary>
    Include = 0,

    /// <summary>Excludes the field entirely from change tracking records.</summary>
    Exclude = 1,

    /// <summary>Records the field name but replaces the value with a redaction indicator.</summary>
    Redact = 2,

    /// <summary>
    /// Replaces the field value with its SHA-256 cryptographic digest to support equality comparison without revealing plaintext.
    /// </summary>
    /// <remarks>
    /// This value is not automatically applied by <see cref="AuditSensitivityPipeline.Apply"/>.
    /// To hash a field value, call <see cref="AuditSensitivityPipeline.HashValue"/> explicitly
    /// and pass the resulting hex string as the change value.
    /// </remarks>
    Hash = 3
}
