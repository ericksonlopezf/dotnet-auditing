// Copyright © Erickson Lopez. MIT License.
using System;
using EricksonLopez.Auditing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EricksonLopez.Auditing.EntityFrameworkCore;

/// <summary>Provides extension methods for registering Entity Framework Core audit persistence services.</summary>
public static class AuditingEfCoreExtensions
{
    /// <summary>Registers <see cref="EfCoreAuditStore"/> as the audit storage provider in the service collection.</summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configureDbContext">A delegate to configure the <see cref="DbContextOptionsBuilder"/> for the audit database context.</param>
    /// <returns>The configured <see cref="IServiceCollection"/> instance.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> or <paramref name="configureDbContext"/> is <see langword="null"/></exception>
    public static IServiceCollection AddEntityFrameworkCoreAuditStore(
        this IServiceCollection services,
        Action<DbContextOptionsBuilder> configureDbContext)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configureDbContext);

        services.AddDbContextFactory<AuditDbContext>(configureDbContext);
        services.AddScoped<IAuditStore, EfCoreAuditStore>();

        return services;
    }
}
