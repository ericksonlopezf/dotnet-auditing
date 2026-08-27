// Copyright © Erickson Lopez. MIT License.
using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace EricksonLopez.Mediator.RateLimiting;

/// <summary>
/// Provides extension methods for registering rate limiting mediator behaviors in dependency injection.
/// </summary>
public static class RateLimitingMediatorExtensions
{
    /// <summary>
    /// Registers the rate limiting pipeline behavior with the service collection.
    /// </summary>
    /// <param name="services">The service collection to register the behavior into.</param>
    /// <returns>The same service collection instance for method chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is <see langword="null"/>.</exception>
    public static IServiceCollection AddMediatorRateLimiting(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddTransient(typeof(IPipelineBehavior<,>), typeof(RateLimitingBehavior<,>));
        return services;
    }
}
