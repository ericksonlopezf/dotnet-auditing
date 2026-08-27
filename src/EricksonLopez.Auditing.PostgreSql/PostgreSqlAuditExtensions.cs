// Copyright © Erickson Lopez. MIT License.
using System;
using Microsoft.Extensions.DependencyInjection;

namespace EricksonLopez.Auditing.PostgreSql;

/// <summary>Provides extension methods for registering PostgreSQL audit persistence services.</summary>
public static class PostgreSqlAuditExtensions
{
    /// <summary>Registers <see cref="PostgreSqlAuditStore"/> as the audit storage provider in the service collection.</summary>
    /// <param name="builder">The audit builder instance being configured.</param>
    /// <param name="configure">A delegate to configure PostgreSQL audit store options.</param>
    /// <returns>The <see cref="IAuditBuilder"/> instance for fluent chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> or <paramref name="configure"/> is <see langword="null"/></exception>
    /// <example>
    /// <code>
    /// services.AddAuditing()
    ///         .UsePostgreSql(options =>
    ///         {
    ///             options.ConnectionFactory = () => new NpgsqlConnection(connectionString);
    ///         });
    /// </code>
    /// </example>
    public static IAuditBuilder UsePostgreSql(
        this IAuditBuilder builder,
        Action<PostgreSqlAuditStoreOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        var optionsBuilder = new PostgreSqlAuditStoreOptionsBuilder();
        configure(optionsBuilder.Options);

        builder.Services.AddSingleton(optionsBuilder.Options);
        builder.Services.AddSingleton<IAuditStore, PostgreSqlAuditStore>();
        builder.Services.AddSingleton<PostgreSqlAuditIntegrityVerifier>();

        return builder;
    }

    private sealed class PostgreSqlAuditStoreOptionsBuilder
    {
        public PostgreSqlAuditStoreOptions Options { get; } = new()
        {
            ConnectionFactory = () => throw new InvalidOperationException(
                "PostgreSqlAuditStoreOptions.ConnectionFactory must be configured. " +
                "Call UsePostgreSql(options => options.ConnectionFactory = () => new NpgsqlConnection(...)).")
        };
    }
}
