// Copyright © Erickson Lopez. MIT License.
using System;

namespace EricksonLopez.Auditing;

/// <summary>Represents the authenticated identity that performed an audited action.</summary>
/// <param name="Type">The classification category of the actor.</param>
/// <param name="Id">The stable identifier of the actor.</param>
/// <param name="DisplayName">The optional human-readable label for the actor.</param>
/// <exception cref="ArgumentException"><paramref name="Id"/> is <see langword="null"/> or white-space</exception>
public sealed record AuditActor(AuditActorType Type, string Id, string? DisplayName = null)
{
    private readonly string _id = !string.IsNullOrWhiteSpace(Id) ? Id : throw new ArgumentException("Actor Id cannot be null or whitespace.", nameof(Id));

    /// <summary>Gets the stable identifier of the actor.</summary>
    /// <exception cref="ArgumentException"><paramref name="value"/> is <see langword="null"/> or white-space</exception>
    public string Id
    {
        get => _id;
        init
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value);
            _id = value;
        }
    }

    /// <summary>Gets a predefined actor representing an anonymous or unauthenticated principal.</summary>
    public static readonly AuditActor Anonymous = new(AuditActorType.Anonymous, "anonymous");

    /// <summary>Gets a predefined actor representing the hosting system process.</summary>
    public static readonly AuditActor System = new(AuditActorType.SystemProcess, "system");
}
