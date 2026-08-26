// Copyright © Erickson Lopez. MIT License.
using System;
using Microsoft.Extensions.DependencyInjection;

namespace EricksonLopez.Auditing.Oracle;

/// <summary>Provides extension methods for registering Oracle Database audit persistence services.</summary>
public static class OracleAuditExtensions
{
    /// <summary>Registers <see cref="OracleAuditStore"/> as the audit storage provider in the service collection.</summary>
    /// <param name="builder">The audit builder instance being configured.</param>
    /// <param name="configure">A delegate to configure Oracle audit store options.</param>
    /// <returns>The <see cref="IAuditBuilder"/> instance for fluent chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> or <paramref name="configure"/> is <see langword="null"/></exception>
    /// <example>
    /// <code>
    /// services.AddAuditing()
    ///         .UseOracle(options =>
    ///         {
    ///             options.ConnectionFactory = () => new OracleConnection(connectionString);
    ///         });
    /// </code>
    /// </example>
    public static IAuditBuilder UseOracle(
        this IAuditBuilder builder,
        Action<OracleAuditStoreOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        var optionsBuilder = new OracleAuditStoreOptionsBuilder();
        configure(optionsBuilder.Options);

        builder.Services.AddSingleton(optionsBuilder.Options);
        builder.Services.AddSingleton<IAuditStore, OracleAuditStore>();
        builder.Services.AddSingleton<OracleAuditIntegrityVerifier>();

        return builder;
    }

    private sealed class OracleAuditStoreOptionsBuilder
    {
        public OracleAuditStoreOptions Options { get; } = new()
        {
            ConnectionFactory = () => throw new InvalidOperationException(
                "OracleAuditStoreOptions.ConnectionFactory must be configured. " +
                "Call UseOracle(options => options.ConnectionFactory = () => new OracleConnection(...)).")
        };
    }
}
