// Copyright © Erickson Lopez. MIT License.

namespace EricksonLopez.Auditing;

/// <summary>Represents the authenticated identity that performed an audited action.</summary>
/// <param name="Type">The classification category of the actor.</param>
/// <param name="Id">The stable identifier of the actor.</param>
/// <param name="DisplayName">The optional human-readable label for the actor.</param>
public sealed record AuditActor(AuditActorType Type, string Id, string? DisplayName = null)
{
    /// <summary>Gets a predefined actor representing an anonymous or unauthenticated principal.</summary>
    public static readonly AuditActor Anonymous = new(AuditActorType.Anonymous, "anonymous");

    /// <summary>Gets a predefined actor representing the hosting system process.</summary>
    public static readonly AuditActor System = new(AuditActorType.SystemProcess, "system");
}
