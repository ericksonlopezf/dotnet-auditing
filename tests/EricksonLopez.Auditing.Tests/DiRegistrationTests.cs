// Copyright © Erickson Lopez. MIT License.
using System;
using System.Linq;
using AwesomeAssertions;
using EricksonLopez.Auditing.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EricksonLopez.Auditing.Tests;

public sealed class DiRegistrationTests
{
    [Fact]
    public void AddAuditing_NullServices_Throws()
    {
        IServiceCollection services = null!;
        Action act = () => services.AddAuditing();
        act.Should().Throw<ArgumentNullException>().WithParameterName("services");
    }

    [Fact]
    public void AddAuditing_NullServices_ThrowsBeforeInvokingConfigure()
    {
        IServiceCollection services = null!;
        bool configureInvoked = false;
        Action act = () => services.AddAuditing(_ => configureInvoked = true);
        act.Should().Throw<ArgumentNullException>().WithParameterName("services");
        configureInvoked.Should().BeFalse("services must be validated before invoking the configure action");
    }

    [Fact]
    public void AddAuditing_RegistersRequiredServices()
    {
        var services = new ServiceCollection();
        var builder = services.AddAuditing();
        builder.Services.Should().BeSameAs(services);

        services.Any(d => d.ServiceType == typeof(AuditConfiguration) && d.Lifetime == ServiceLifetime.Singleton).Should().BeTrue();
        services.Any(d => d.ServiceType == typeof(AuditSensitivityPipeline) && d.Lifetime == ServiceLifetime.Singleton).Should().BeTrue();
        services.Any(d => d.ServiceType == typeof(IAuditActorProvider) && d.Lifetime == ServiceLifetime.Singleton && d.ImplementationInstance == SystemAuditActorProvider.Instance).Should().BeTrue();

        var provider = services.BuildServiceProvider();

        provider.GetService<AuditConfiguration>().Should().NotBeNull();
        provider.GetService<AuditSensitivityPipeline>().Should().NotBeNull();
        provider.GetService<IAuditActorProvider>().Should().BeOfType<SystemAuditActorProvider>();
    }

    [Fact]
    public void EnableIntegrityChain_SetsConfigTrue()
    {
        var services = new ServiceCollection();
        var builder = services.AddAuditing();
        var returnedBuilder = builder.EnableIntegrityChain();
        returnedBuilder.Should().BeSameAs(builder);

        services.Any(d => d.ServiceType == typeof(HmacAuditIntegrityService)).Should().BeTrue();

        var provider = services.BuildServiceProvider();
        provider.GetRequiredService<AuditConfiguration>().EnableIntegrityChain.Should().BeTrue();
    }

    [Fact]
    public void EnableIntegrityChain_WithoutConfig_Throws()
    {
        var services = new ServiceCollection();
        var builder = services.AddAuditing();

        // Remove config to simulate incorrect manual registration
        var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(AuditConfiguration));
        if (descriptor != null) services.Remove(descriptor);

        Action act = () => builder.EnableIntegrityChain();
        act.Should().Throw<InvalidOperationException>().WithMessage("*AddAuditing*");
    }

    [Fact]
    public void UseStore_RegistersCustomStore()
    {
        var services = new ServiceCollection();
        var builder = services.AddAuditing();
        var returnedBuilder = builder.UseStore<InMemoryAuditStore>();
        returnedBuilder.Should().BeSameAs(builder);

        services.Any(d => d.ServiceType == typeof(IAuditStore) && d.ImplementationType == typeof(InMemoryAuditStore)).Should().BeTrue();

        var provider = services.BuildServiceProvider();
        provider.GetService<IAuditStore>().Should().BeOfType<InMemoryAuditStore>();
    }

    [Fact]
    public void AddAuditing_WithCustomActorProvider_OverridesDefault()
    {
        var services = new ServiceCollection();
        var builder = services.AddAuditing();
        var returnedBuilder = builder.UseActorProvider<CustomActorProviderStub>();
        returnedBuilder.Should().BeSameAs(builder);

        services.Any(d => d.ServiceType == typeof(IAuditActorProvider) && d.ImplementationType == typeof(CustomActorProviderStub)).Should().BeTrue();

        var provider = services.BuildServiceProvider();
        provider.GetService<IAuditActorProvider>().Should().BeOfType<CustomActorProviderStub>();
    }

    [Fact]
    public void AddAuditing_WithCustomConfiguration_AppliesSettings()
    {
        var services = new ServiceCollection();
        services.AddAuditing(cfg =>
        {
            cfg.DefaultFailureBehavior = AuditFailureBehavior.FailOpen;
            cfg.EnableIntegrityChain = false;
        });

        var provider = services.BuildServiceProvider();
        var config = provider.GetRequiredService<AuditConfiguration>();

        config.DefaultFailureBehavior.Should().Be(AuditFailureBehavior.FailOpen);
    }

    [Fact]
    public void AddAuditing_NoDefaultStore_IAuditStoreNotRegistered()
    {
        var services = new ServiceCollection();
        services.AddAuditing();
        var provider = services.BuildServiceProvider();

        // No IAuditStore should be registered by default — no silent InMemory drop
        provider.GetService<IAuditStore>().Should().BeNull(
            "no default audit store should be registered; consumers must configure one explicitly");
    }

    private sealed class CustomActorProviderStub : IAuditActorProvider
    {
        public AuditActor GetCurrentActor() => AuditActor.Anonymous;
    }
}
