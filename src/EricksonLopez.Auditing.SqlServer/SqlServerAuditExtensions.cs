// Copyright © Erickson Lopez. MIT License.
using System;
using Microsoft.Extensions.DependencyInjection;

namespace EricksonLopez.Auditing.SqlServer;

/// <summary>Provides extension methods for registering Microsoft SQL Server audit persistence services.</summary>
public static class SqlServerAuditExtensions
{
    /// <summary>Registers <see cref="SqlServerAuditStore"/> as the audit storage provider in the service collection.</summary>
    /// <param name="builder">The audit builder instance being configured.</param>
    /// <param name="configure">A delegate to configure SQL Server audit store options.</param>
    /// <returns>The <see cref="IAuditBuilder"/> instance for fluent chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> or <paramref name="configure"/> is <see langword="null"/></exception>
    /// <example>
    /// <code>
    /// services.AddAuditing()
    ///         .UseSqlServer(options =>
    ///         {
    ///             options.ConnectionFactory = () => new SqlConnection(connectionString);
    ///         });
    /// </code>
    /// </example>
    public static IAuditBuilder UseSqlServer(
        this IAuditBuilder builder,
        Action<SqlServerAuditStoreOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        var optionsBuilder = new SqlServerAuditStoreOptionsBuilder();
        configure(optionsBuilder.Options);

        builder.Services.AddSingleton(optionsBuilder.Options);
        builder.Services.AddSingleton<IAuditStore, SqlServerAuditStore>();
        builder.Services.AddSingleton<SqlServerAuditIntegrityVerifier>();

        return builder;
    }

    private sealed class SqlServerAuditStoreOptionsBuilder
    {
        public SqlServerAuditStoreOptions Options { get; } = new()
        {
            ConnectionFactory = () => throw new InvalidOperationException(
                "SqlServerAuditStoreOptions.ConnectionFactory must be configured. " +
                "Call UseSqlServer(options => options.ConnectionFactory = () => new SqlConnection(...)).")
        };
    }
}
