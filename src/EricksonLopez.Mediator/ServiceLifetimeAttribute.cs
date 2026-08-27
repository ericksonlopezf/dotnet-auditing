// Copyright © Erickson Lopez. MIT License.
using System;

namespace EricksonLopez.Mediator;

/// <summary>
/// Specifies the service lifetime of the decorated handler within the dependency injection container.
/// </summary>
/// <remarks>
/// When this attribute is omitted, handlers default to <see cref="HandlerLifetime.Transient"/>.
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public sealed class ServiceLifetimeAttribute : Attribute
{
    /// <summary>
    /// Gets the configured lifetime for the handler registration.
    /// </summary>
    public HandlerLifetime Lifetime { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ServiceLifetimeAttribute"/> class
    /// with the specified service lifetime.
    /// </summary>
    /// <param name="lifetime">The service lifetime to apply to the handler registration.</param>
    public ServiceLifetimeAttribute(HandlerLifetime lifetime)
    {
        Lifetime = lifetime;
    }
}
