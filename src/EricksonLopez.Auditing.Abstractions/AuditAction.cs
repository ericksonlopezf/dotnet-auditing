// Copyright © Erickson Lopez. MIT License.

namespace EricksonLopez.Auditing;

/// <summary>Represents an operation executed by an actor on a resource.</summary>
/// <param name="Code">A short code identifying the action.</param>
public readonly record struct AuditAction(string Code)
{
    /// <summary>Represents an action where a new resource was created.</summary>
    public static readonly AuditAction Create = new("Create");

    /// <summary>Represents an action where an existing resource was updated.</summary>
    public static readonly AuditAction Update = new("Update");

    /// <summary>Represents an action where an existing resource was deleted or soft-deleted.</summary>
    public static readonly AuditAction Delete = new("Delete");

    /// <summary>Represents an action where a resource was accessed or read.</summary>
    public static readonly AuditAction Read = new("Read");

    /// <summary>Represents an action where a resource or request was approved.</summary>
    public static readonly AuditAction Approve = new("Approve");

    /// <summary>Represents an action where a resource or request was rejected.</summary>
    public static readonly AuditAction Reject = new("Reject");

    /// <summary>Represents an action where a user authenticated into the system.</summary>
    public static readonly AuditAction Login = new("Login");

    /// <summary>Represents an action where a user explicitly ended their session.</summary>
    public static readonly AuditAction Logout = new("Logout");

    /// <summary>Represents an action where data was exported outside the system boundary.</summary>
    public static readonly AuditAction Export = new("Export");

    /// <summary>Represents an action where a file or resource was downloaded.</summary>
    public static readonly AuditAction Download = new("Download");

    /// <summary>Represents an action where a resource was sent or dispatched.</summary>
    public static readonly AuditAction Send = new("Send");

    /// <summary>Represents an action where a pending operation was cancelled.</summary>
    public static readonly AuditAction Cancel = new("Cancel");

    /// <summary>Represents an action where a resource was restored after soft-deletion.</summary>
    public static readonly AuditAction Restore = new("Restore");

    /// <summary>Represents an action where a permission or role was assigned.</summary>
    public static readonly AuditAction GrantPermission = new("GrantPermission");

    /// <summary>Represents an action where a permission or role was revoked.</summary>
    public static readonly AuditAction RevokePermission = new("RevokePermission");

    /// <inheritdoc/>
    public override string ToString() => Code;
}
