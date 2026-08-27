// Copyright © Erickson Lopez. MIT License.

namespace EricksonLopez.Mediator;

/// <summary>
/// Specifies the lifetime of a handler registration within the dependency injection container.
/// </summary>
public enum HandlerLifetime
{
    /// <summary>
    /// Specifies that a single instance of the handler is created and shared across all requests.
    /// </summary>
    Singleton,

    /// <summary>
    /// Specifies that a new instance of the handler is created per service scope.
    /// </summary>
    Scoped,

    /// <summary>
    /// Specifies that a new instance of the handler is created each time it is requested.
    /// </summary>
    Transient
}
