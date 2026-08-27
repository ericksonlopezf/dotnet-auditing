// Copyright © Erickson Lopez. MIT License.

namespace EricksonLopez.Auditing;

/// <summary>Specifies the observable result of an audited action.</summary>
public enum AuditOutcome : byte
{
    /// <summary>Specifies that the action completed successfully.</summary>
    Success = 1,

    /// <summary>Specifies that the action failed due to a runtime or validation error.</summary>
    Failure = 2,

    /// <summary>Specifies that the action was rejected due to insufficient permissions.</summary>
    Denied = 3,

    /// <summary>Specifies that the action was cancelled before completion.</summary>
    Cancelled = 4,

    /// <summary>Specifies that the action completed partially with mixed results.</summary>
    Partial = 5
}
