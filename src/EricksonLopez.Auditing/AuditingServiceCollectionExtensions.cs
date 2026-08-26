// Copyright © Erickson Lopez. MIT License.
using System;
using Microsoft.Extensions.DependencyInjection;

namespace EricksonLopez.Auditing;

/// <summary>Provides extension methods for registering core auditing services into an <see cref="IServiceCollection"/>.</summary>
public static class AuditingServiceCollectionExtensions
{
    /// <summary>Adds core auditing services, configuration, pipeline, and default providers to the service collection.</summary>
    /// <param name="services">The service collection to register into.</param>
    /// <param name="configure">An optional action to configure auditing policies.</param>
    /// <returns>An <see cref="IAuditBuilder"/> instance for configuring storage and providers.</returns>
    public static IAuditBuilder AddAuditing(this IServiceCollection services, Action<AuditConfiguration>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var config = new AuditConfiguration();
        configure?.Invoke(config);

        services.AddSingleton(config);
        services.AddSingleton<AuditSensitivityPipeline>();
        services.AddSingleton<IAuditActorProvider>(SystemAuditActorProvider.Instance);

        return new AuditBuilder(services);
    }
}
