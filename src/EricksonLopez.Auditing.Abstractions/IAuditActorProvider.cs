// Copyright © Erickson Lopez. MIT License.

namespace EricksonLopez.Auditing;

/// <summary>Defines a provider for resolving the current authenticated actor identity.</summary>
public interface IAuditActorProvider
{
    /// <summary>Resolves the current authenticated actor identity.</summary>
    /// <returns>The <see cref="AuditActor"/> performing the current operation, or <see cref="AuditActor.Anonymous"/> if unauthenticated.</returns>
    AuditActor GetCurrentActor();
}
