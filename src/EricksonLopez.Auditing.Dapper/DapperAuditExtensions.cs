// Copyright © Erickson Lopez. MIT License.
using System;
using Microsoft.Extensions.DependencyInjection;

namespace EricksonLopez.Auditing.Dapper;

/// <summary>Provides extension methods for registering Dapper-based audit persistence services.</summary>
public static class DapperAuditExtensions
{
    /// <summary>Registers <see cref="DapperAuditStore"/> as the audit storage provider in the service collection.</summary>
    /// <param name="builder">The audit builder instance being configured.</param>
    /// <param name="configure">A delegate to configure the Dapper audit store options.</param>
    /// <returns>The <see cref="IAuditBuilder"/> instance for fluent chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> or <paramref name="configure"/> is <see langword="null"/></exception>
    /// <exception cref="InvalidOperationException"><see cref="DapperAuditStoreOptions.ConnectionFactory"/> was not configured</exception>
    /// <example>
    /// <code>
    /// services.AddAuditing()
    ///         .UseDapper(options =>
    ///         {
    ///             options.ConnectionFactory = () => new SqlConnection(connectionString);
    ///         });
    /// </code>
    /// </example>
    public static IAuditBuilder UseDapper(
        this IAuditBuilder builder,
        Action<DapperAuditStoreOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        var options = new DapperAuditStoreOptions();
        configure(options);

        if (options.ConnectionFactory is null)
        {
            throw new InvalidOperationException(
                "DapperAuditStoreOptions.ConnectionFactory must be configured. " +
                "Call UseDapper(options => options.ConnectionFactory = () => new DbConnection(...)).");
        }

        builder.Services.AddSingleton(options);
        builder.Services.AddSingleton<IAuditStore, DapperAuditStore>();

        return builder;
    }
}
