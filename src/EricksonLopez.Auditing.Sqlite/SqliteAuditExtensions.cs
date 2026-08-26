// Copyright © Erickson Lopez. MIT License.
using System;
using Microsoft.Extensions.DependencyInjection;

namespace EricksonLopez.Auditing.Sqlite;

/// <summary>Provides extension methods for registering SQLite audit persistence services.</summary>
public static class SqliteAuditExtensions
{
    /// <summary>Registers <see cref="SqliteAuditStore"/> as the audit storage provider in the service collection.</summary>
    /// <param name="builder">The audit builder instance being configured.</param>
    /// <param name="configure">A delegate to configure SQLite audit store options.</param>
    /// <returns>The <see cref="IAuditBuilder"/> instance for fluent chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> or <paramref name="configure"/> is <see langword="null"/></exception>
    /// <example>
    /// <code>
    /// services.AddAuditing()
    ///         .UseSqlite(options =>
    ///         {
    ///             options.ConnectionFactory = () => new SqliteConnection("Data Source=audit.db");
    ///         });
    /// </code>
    /// </example>
    public static IAuditBuilder UseSqlite(
        this IAuditBuilder builder,
        Action<SqliteAuditStoreOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        var optionsBuilder = new SqliteAuditStoreOptionsBuilder();
        configure(optionsBuilder.Options);

        builder.Services.AddSingleton(optionsBuilder.Options);
        builder.Services.AddSingleton<IAuditStore, SqliteAuditStore>();
        builder.Services.AddSingleton<SqliteAuditIntegrityVerifier>();

        return builder;
    }

    private sealed class SqliteAuditStoreOptionsBuilder
    {
        public SqliteAuditStoreOptions Options { get; } = new()
        {
            ConnectionFactory = () => throw new InvalidOperationException(
                "SqliteAuditStoreOptions.ConnectionFactory must be configured. " +
                "Call UseSqlite(options => options.ConnectionFactory = () => new SqliteConnection(...)).")
        };
    }
}
