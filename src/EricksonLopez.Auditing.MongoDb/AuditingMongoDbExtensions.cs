// Copyright © Erickson Lopez. MIT License.
using System;
using EricksonLopez.Auditing;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;

namespace EricksonLopez.Auditing.MongoDb;

/// <summary>Provides extension methods for registering MongoDB audit persistence services.</summary>
public static class AuditingMongoDbExtensions
{
    /// <summary>Registers <see cref="MongoAuditStore"/> as the audit storage provider in the service collection.</summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="databaseFactory">A factory function that resolves the target <see cref="IMongoDatabase"/> instance.</param>
    /// <param name="configure">An optional delegate to configure MongoDB audit store options.</param>
    /// <returns>The configured <see cref="IServiceCollection"/> instance.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> or <paramref name="databaseFactory"/> is <see langword="null"/></exception>
    public static IServiceCollection AddMongoDbAuditStore(
        this IServiceCollection services,
        Func<IServiceProvider, IMongoDatabase> databaseFactory,
        Action<MongoAuditStoreOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(databaseFactory);

        var options = new MongoAuditStoreOptions();
        configure?.Invoke(options);

        services.AddSingleton(options);
        services.AddScoped<IAuditStore>(sp =>
        {
            var db = databaseFactory(sp);
            return new MongoAuditStore(db, options);
        });

        return services;
    }
}
