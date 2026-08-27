// Copyright © Erickson Lopez. MIT License.

namespace EricksonLopez.Auditing;

/// <summary>Provides a default actor provider that returns <see cref="AuditActor.System"/> for background operations.</summary>
public sealed class SystemAuditActorProvider : IAuditActorProvider
{
    /// <summary>Gets the singleton instance of <see cref="SystemAuditActorProvider"/>.</summary>
    public static readonly SystemAuditActorProvider Instance = new();

    private SystemAuditActorProvider() { }

    /// <inheritdoc/>
    public AuditActor GetCurrentActor() => AuditActor.System;
}
