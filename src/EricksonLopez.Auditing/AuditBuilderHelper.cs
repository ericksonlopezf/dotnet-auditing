// Copyright © Erickson Lopez. MIT License.
using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace EricksonLopez.Auditing;

internal static class AuditBuilderHelper
{
    public static void ApplyDecorators(IAuditBuilder builder)
    {
        var services = builder.Services;

        AuditConfiguration? config = null;
        for (int i = services.Count - 1; i >= 0; i--)
        {
            if (services[i].ServiceType == typeof(AuditConfiguration) &&
                services[i].ImplementationInstance is AuditConfiguration c)
            {
                config = c;
                break;
            }
        }

        BufferedAuditStoreOptions? bufferOptions = null;
        for (int i = services.Count - 1; i >= 0; i--)
        {
            if (services[i].ServiceType == typeof(BufferedAuditStoreOptions) &&
                services[i].ImplementationInstance is BufferedAuditStoreOptions bo)
            {
                bufferOptions = bo;
                break;
            }
        }

        int storeIndex = -1;
        for (int i = services.Count - 1; i >= 0; i--)
        {
            if (services[i].ServiceType == typeof(IAuditStore))
            {
                storeIndex = i;
                break;
            }
        }

        if (storeIndex < 0) return;

        var descriptor = services[storeIndex];

        // 1. Apply Integrity Chain if enabled and not already applied
        if (config != null && config.EnableIntegrityChain)
        {
            services.TryAddSingleton<IAuditHashAlgorithm, HmacSha256AuditHashAlgorithm>();
            services.TryAddSingleton<HmacAuditIntegrityService>();

            descriptor = WrapWithIntegrity(services, descriptor);
            services[storeIndex] = descriptor;
        }

        // 2. Apply Buffering if enabled and not already applied
        if (bufferOptions != null)
        {
            descriptor = WrapWithBuffering(services, descriptor, bufferOptions);
            services[storeIndex] = descriptor;
        }
    }

    private static ServiceDescriptor WrapWithIntegrity(IServiceCollection services, ServiceDescriptor descriptor)
    {
        if (descriptor.ImplementationInstance is IAuditStore instance)
        {
            if (instance is IntegrityAuditStoreDecorator) return descriptor;
            return ServiceDescriptor.Singleton<IAuditStore>(sp =>
                new IntegrityAuditStoreDecorator(instance, sp.GetRequiredService<HmacAuditIntegrityService>()));
        }
        if (descriptor.ImplementationFactory != null)
        {
            var factory = descriptor.ImplementationFactory;
            return ServiceDescriptor.Singleton<IAuditStore>(sp =>
            {
                var inner = (IAuditStore)factory(sp);
                if (inner is IntegrityAuditStoreDecorator) return inner;
                return new IntegrityAuditStoreDecorator(inner, sp.GetRequiredService<HmacAuditIntegrityService>());
            });
        }
        if (descriptor.ImplementationType != null)
        {
            var implType = descriptor.ImplementationType;
            if (implType == typeof(IntegrityAuditStoreDecorator)) return descriptor;
            services.TryAddSingleton(implType);
            return ServiceDescriptor.Singleton<IAuditStore>(sp =>
                new IntegrityAuditStoreDecorator((IAuditStore)sp.GetRequiredService(implType), sp.GetRequiredService<HmacAuditIntegrityService>()));
        }
        // Stryker disable once all : Fallback return for unhandled ServiceDescriptor shape
        return descriptor;
    }

    private static ServiceDescriptor WrapWithBuffering(IServiceCollection services, ServiceDescriptor descriptor, BufferedAuditStoreOptions options)
    {
        if (descriptor.ImplementationInstance is IAuditStore instance)
        {
            if (instance is BufferedAuditStoreDecorator) return descriptor;
            return ServiceDescriptor.Singleton<IAuditStore>(sp =>
                new BufferedAuditStoreDecorator(instance, options, sp.GetService<Microsoft.Extensions.Logging.ILogger<BufferedAuditStoreDecorator>>()));
        }
        if (descriptor.ImplementationFactory != null)
        {
            var factory = descriptor.ImplementationFactory;
            return ServiceDescriptor.Singleton<IAuditStore>(sp =>
            {
                var inner = (IAuditStore)factory(sp);
                if (inner is BufferedAuditStoreDecorator) return inner;
                return new BufferedAuditStoreDecorator(inner, options, sp.GetService<Microsoft.Extensions.Logging.ILogger<BufferedAuditStoreDecorator>>());
            });
        }
        if (descriptor.ImplementationType != null)
        {
            var implType = descriptor.ImplementationType;
            if (implType == typeof(BufferedAuditStoreDecorator)) return descriptor;
            services.TryAddSingleton(implType);
            return ServiceDescriptor.Singleton<IAuditStore>(sp =>
                new BufferedAuditStoreDecorator((IAuditStore)sp.GetRequiredService(implType), options, sp.GetService<Microsoft.Extensions.Logging.ILogger<BufferedAuditStoreDecorator>>()));
        }
        // Stryker disable once all : Fallback return for unhandled ServiceDescriptor shape
        return descriptor;
    }
}
