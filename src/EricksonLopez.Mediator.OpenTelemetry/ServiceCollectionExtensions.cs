// Copyright © Erickson Lopez. MIT License.
using System;
using EricksonLopez.Mediator.OpenTelemetry;
using Microsoft.Extensions.DependencyInjection;

namespace EricksonLopez.Mediator.OpenTelemetry;

/// <summary>
/// Provides extension methods for configuring OpenTelemetry instrumentation with dependency injection.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers and configures OpenTelemetry instrumentation options for the mediator pipeline.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configure">An optional delegate used to configure <see cref="MediatorOpenTelemetryOptions"/>.</param>
    /// <returns>The same service collection instance for method chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is <see langword="null"/>.</exception>
    public static IServiceCollection AddMediatorOpenTelemetry(
        this IServiceCollection services,
        Action<MediatorOpenTelemetryOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = new MediatorOpenTelemetryOptions();
        configure?.Invoke(options);

        services.Add(ServiceDescriptor.Singleton(options));
        return services;
    }
}
