// Copyright © Erickson Lopez. MIT License.
using System;
using global::Polly;
using global::Polly.Registry;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace EricksonLopez.Mediator.Polly;

/// <summary>
/// Provides extension methods for configuring Polly resilience pipelines in dependency injection.
/// </summary>
public static class MediatorPollyExtensions
{
    /// <summary>
    /// Registers the open generic <see cref="PollyResilienceBehavior{TRequest, TResponse}"/> with the service collection.
    /// </summary>
    /// <param name="services">The service collection to register the behavior into.</param>
    /// <returns>The same service collection instance for method chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is <see langword="null"/>.</exception>
    public static IServiceCollection AddMediatorPolly(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddTransient(typeof(PollyResilienceBehavior<,>));
        return services;
    }

    /// <summary>
    /// Configures and registers a default Polly resilience pipeline for the mediator.
    /// </summary>
    /// <param name="services">The service collection to register the pipeline into.</param>
    /// <param name="configure">A delegate used to configure the resilience pipeline builder.</param>
    /// <returns>The same service collection instance for method chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> or <paramref name="configure"/> is <see langword="null"/>.</exception>
    public static IServiceCollection AddMediatorDefaultResiliencePipeline(
        this IServiceCollection services,
        Action<ResiliencePipelineBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var builder = new ResiliencePipelineBuilder();
        configure(builder);
        var pipeline = builder.Build();

        services.Add(ServiceDescriptor.Singleton(typeof(ResiliencePipeline), pipeline));
        services.AddMediatorPolly();
        return services;
    }
}
