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
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is <see langword="null"/></exception>
    public static IAuditBuilder AddAuditing(this IServiceCollection services, Action<AuditConfiguration>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var config = new AuditConfiguration();
        configure?.Invoke(config);

        services.AddSingleton(config);
        services.AddSingleton<IAuditSensitivityPipeline, AuditSensitivityPipeline>();
        services.AddSingleton<AuditSensitivityPipeline>(sp => (AuditSensitivityPipeline)sp.GetRequiredService<IAuditSensitivityPipeline>());
        services.AddSingleton<IAuditActorProvider>(SystemAuditActorProvider.Instance);
        services.AddSingleton(typeof(IAuditLogger<>), typeof(AuditLogger<>));

        return new AuditBuilder(services);
    }

    /// <summary>
    /// Decorates the registered <see cref="IAuditStore"/> with a high-throughput asynchronous batching buffer (<see cref="BufferedAuditStoreDecorator"/>).
    /// </summary>
    /// <param name="builder">The audit builder being configured.</param>
    /// <param name="configure">An optional configuration delegate to customize buffer capacity, batch size, and flush interval.</param>
    /// <returns>The <see cref="IAuditBuilder"/> instance for fluent method chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> is <see langword="null"/></exception>
    public static IAuditBuilder EnableBuffering(this IAuditBuilder builder, Action<BufferedAuditStoreOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var options = new BufferedAuditStoreOptions();
        configure?.Invoke(options);

        for (int i = builder.Services.Count - 1; i >= 0; i--)
        {
            if (builder.Services[i].ServiceType == typeof(BufferedAuditStoreOptions))
            {
                builder.Services.RemoveAt(i);
            }
        }
        builder.Services.AddSingleton(options);

        AuditBuilderHelper.ApplyDecorators(builder);
        return builder;
    }

    /// <summary>
    /// Applies any pending decorators (HMAC integrity verification, high-throughput buffering) to the registered audit store.
    /// </summary>
    /// <param name="builder">The audit builder being configured.</param>
    /// <returns>The <see cref="IAuditBuilder"/> instance for fluent method chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> is <see langword="null"/></exception>
    public static IAuditBuilder ApplyDecorators(this IAuditBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        AuditBuilderHelper.ApplyDecorators(builder);
        return builder;
    }
}
