// Copyright © Erickson Lopez. MIT License.
using System;
using EricksonLopez.Mediator.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace EricksonLopez.Mediator;

/// <summary>
/// Provides extension methods for registering <see cref="MediatorHealthCheck"/> with dependency injection.
/// </summary>
public static class MediatorHealthCheckExtensions
{
    /// <summary>
    /// Registers <see cref="MediatorHealthCheck"/> with the specified service collection.
    /// </summary>
    /// <param name="services">The service collection to register the health check into.</param>
    /// <returns>The same service collection instance for method chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is <see langword="null"/>.</exception>
    public static IServiceCollection AddMediatorHealthCheck(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.Add(ServiceDescriptor.Transient<IHealthCheck, MediatorHealthCheck>());
        return services;
    }
}
