// Copyright © Erickson Lopez. MIT License.
using System;
using Microsoft.Extensions.DependencyInjection;

namespace EricksonLopez.Auditing.MySql;

/// <summary>Provides extension methods for registering MySQL and MariaDB audit persistence services.</summary>
public static class MySqlAuditExtensions
{
    /// <summary>Registers <see cref="MySqlAuditStore"/> as the audit storage provider in the service collection.</summary>
    /// <param name="builder">The audit builder instance being configured.</param>
    /// <param name="configure">A delegate to configure MySQL audit store options.</param>
    /// <returns>The <see cref="IAuditBuilder"/> instance for fluent chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> or <paramref name="configure"/> is <see langword="null"/></exception>
    /// <example>
    /// <code>
    /// services.AddAuditing()
    ///         .UseMySql(options =>
    ///         {
    ///             options.ConnectionFactory = () => new MySqlConnection(connectionString);
    ///         });
    /// </code>
    /// </example>
    public static IAuditBuilder UseMySql(
        this IAuditBuilder builder,
        Action<MySqlAuditStoreOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        var optionsBuilder = new MySqlAuditStoreOptionsBuilder();
        configure(optionsBuilder.Options);

        builder.Services.AddSingleton(optionsBuilder.Options);
        builder.Services.AddSingleton<IAuditStore, MySqlAuditStore>();
        builder.Services.AddSingleton<MySqlAuditIntegrityVerifier>();

        return builder;
    }

    private sealed class MySqlAuditStoreOptionsBuilder
    {
        public MySqlAuditStoreOptions Options { get; } = new()
        {
            ConnectionFactory = () => throw new InvalidOperationException(
                "MySqlAuditStoreOptions.ConnectionFactory must be configured. " +
                "Call UseMySql(options => options.ConnectionFactory = () => new MySqlConnection(...)).")
        };
    }
}
