// Copyright © Erickson Lopez. MIT License.
using System;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace EricksonLopez.Auditing;

internal sealed class AuditBuilder : IAuditBuilder
{
    public AuditBuilder(IServiceCollection services) => Services = services;

    public IServiceCollection Services { get; }

    public IAuditBuilder UseActorProvider<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TProvider>()
        where TProvider : class, IAuditActorProvider
    {
        Services.AddSingleton<IAuditActorProvider, TProvider>();
        return this;
    }

    public IAuditBuilder EnableIntegrityChain()
    {
        var config = GetOrThrowConfig();
        config.EnableIntegrityChain = true;
        Services.TryAddSingleton<HmacAuditIntegrityService>();
        return this;
    }

    public IAuditBuilder UseStore<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TStore>()
        where TStore : class, IAuditStore
    {
        Services.AddSingleton<IAuditStore, TStore>();
        return this;
    }

    private AuditConfiguration GetOrThrowConfig()
    {
        for (int i = Services.Count - 1; i >= 0; i--)
        {
            if (Services[i].ServiceType == typeof(AuditConfiguration) &&
                Services[i].ImplementationInstance is AuditConfiguration cfg)
            {
                return cfg;
            }
        }
        throw new InvalidOperationException(
            "AuditConfiguration not found. Ensure AddAuditing() was called before EnableIntegrityChain().");
    }
}
