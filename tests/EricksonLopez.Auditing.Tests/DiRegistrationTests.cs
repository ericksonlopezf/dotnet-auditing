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

    [Fact]
    public void EnableBuffering_Before_UseStore_AppliesDecorator()
    {
        var services = new ServiceCollection();
        services.AddAuditing()
            .EnableBuffering()
            .UseStore<InMemoryAuditStore>();

        var provider = services.BuildServiceProvider();
        var store = provider.GetRequiredService<IAuditStore>();
        store.Should().BeOfType<BufferedAuditStoreDecorator>();
    }

    [Fact]
    public void EnableBuffering_After_UseStore_AppliesDecorator()
    {
        var services = new ServiceCollection();
        services.AddAuditing()
            .UseStore<InMemoryAuditStore>()
            .EnableBuffering();

        var provider = services.BuildServiceProvider();
        var store = provider.GetRequiredService<IAuditStore>();
        store.Should().BeOfType<BufferedAuditStoreDecorator>();
    }

    [Fact]
    public void EnableIntegrityChain_Before_UseStore_AppliesDecorator()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IAuditIntegrityProvider>(new TestAuditIntegrityProvider());
        services.AddAuditing()
            .EnableIntegrityChain()
            .UseStore<InMemoryAuditStore>();

        var provider = services.BuildServiceProvider();
        var store = provider.GetRequiredService<IAuditStore>();
        store.Should().BeOfType<IntegrityAuditStoreDecorator>();
    }

    [Fact]
    public void EnableIntegrityChain_After_UseStore_AppliesDecorator()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IAuditIntegrityProvider>(new TestAuditIntegrityProvider());
        services.AddAuditing()
            .UseStore<InMemoryAuditStore>()
            .EnableIntegrityChain();

        var provider = services.BuildServiceProvider();
        var store = provider.GetRequiredService<IAuditStore>();
        store.Should().BeOfType<IntegrityAuditStoreDecorator>();
    }

    [Fact]
    public void EnableBuffering_And_EnableIntegrityChain_ProducesCorrectChain()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IAuditIntegrityProvider>(new TestAuditIntegrityProvider());
        services.AddAuditing()
            .EnableBuffering()
            .EnableIntegrityChain()
            .UseStore<InMemoryAuditStore>();

        var provider = services.BuildServiceProvider();
        var store = provider.GetRequiredService<IAuditStore>();
        store.Should().BeOfType<BufferedAuditStoreDecorator>();
    }

    [Fact]
    public void EnableBuffering_WithOptions_ConfiguresOptions()
    {
        var services = new ServiceCollection();
        services.AddAuditing()
            .EnableBuffering(opt => opt.Capacity = 42)
            .UseStore<InMemoryAuditStore>();

        var provider = services.BuildServiceProvider();
        provider.GetRequiredService<BufferedAuditStoreOptions>().Capacity.Should().Be(42);
    }

    [Fact]
    public void EnableBuffering_CalledMultipleTimes_ReplacesOldOptions()
    {
        var services = new ServiceCollection();
        services.AddAuditing()
            .EnableBuffering(opt => opt.Capacity = 100)
            .EnableBuffering(opt => opt.Capacity = 200)
            .UseStore<InMemoryAuditStore>();

        services.Count(d => d.ServiceType == typeof(BufferedAuditStoreOptions)).Should().Be(1);
        var provider = services.BuildServiceProvider();
        provider.GetRequiredService<BufferedAuditStoreOptions>().Capacity.Should().Be(200);
    }

    [Fact]
    public void EnableBuffering_NullBuilder_ThrowsArgumentNullException()
    {
        IAuditBuilder builder = null!;
        Action act = () => builder.EnableBuffering();
        act.Should().Throw<ArgumentNullException>().WithParameterName("builder");
    }

    [Fact]
    public void ApplyDecorators_NullBuilder_ThrowsArgumentNullException()
    {
        IAuditBuilder builder = null!;
        Action act = () => builder.ApplyDecorators();
        act.Should().Throw<ArgumentNullException>().WithParameterName("builder");
    }

    [Fact]
    public void EnableIntegrityChain_RegistersHashAlgorithmAndIntegrityService()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IAuditIntegrityProvider>(new TestAuditIntegrityProvider());
        services.AddAuditing().EnableIntegrityChain();

        var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IAuditHashAlgorithm>().Should().BeOfType<HmacSha256AuditHashAlgorithm>();
        provider.GetRequiredService<HmacAuditIntegrityService>().Should().NotBeNull();
    }

    [Fact]
    public void Decorators_WithImplementationInstance_WrapsCorrectlyAndIdempotent()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IAuditIntegrityProvider>(new TestAuditIntegrityProvider());
        var instance = new InMemoryAuditStore();
        services.AddSingleton<IAuditStore>(instance);

        var builder = services.AddAuditing()
            .EnableIntegrityChain()
            .EnableBuffering();

        // Idempotency: call again
        builder.EnableIntegrityChain();
        builder.EnableBuffering();
        builder.ApplyDecorators();

        var provider = services.BuildServiceProvider();
        var store = provider.GetRequiredService<IAuditStore>();
        store.Should().BeOfType<BufferedAuditStoreDecorator>();
    }

    [Fact]
    public void Decorators_WithImplementationFactory_WrapsCorrectlyAndIdempotent()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IAuditIntegrityProvider>(new TestAuditIntegrityProvider());
        services.AddSingleton<IAuditStore>(sp => new InMemoryAuditStore());

        var builder = services.AddAuditing()
            .EnableIntegrityChain()
            .EnableBuffering();

        // Idempotency: call again
        builder.EnableIntegrityChain();
        builder.EnableBuffering();
        builder.ApplyDecorators();

        var provider = services.BuildServiceProvider();
        var store = provider.GetRequiredService<IAuditStore>();
        store.Should().BeOfType<BufferedAuditStoreDecorator>();
    }

    [Fact]
    public void Decorators_WithImplementationType_Idempotency()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IAuditIntegrityProvider>(new TestAuditIntegrityProvider());
        var builder = services.AddAuditing()
            .UseStore<InMemoryAuditStore>()
            .EnableIntegrityChain()
            .EnableBuffering();

        // Call again to verify descriptor.ImplementationType == typeof(...) guards
        builder.ApplyDecorators();

        var provider = services.BuildServiceProvider();
        var store = provider.GetRequiredService<IAuditStore>();
        store.Should().BeOfType<BufferedAuditStoreDecorator>();
    }

    [Fact]
    public void AuditBuilderHelper_StoreRegisteredAtFirstIndex_FindsIndexZero()
    {
        var services = new ServiceCollection();
        // Registered at index 0, BEFORE AddAuditing
        services.AddSingleton<IAuditStore, InMemoryAuditStore>();

        services.AddAuditing().EnableBuffering();

        var provider = services.BuildServiceProvider();
        var store = provider.GetRequiredService<IAuditStore>();
        store.Should().BeOfType<BufferedAuditStoreDecorator>();
    }

    [Fact]
    public void AuditBuilderHelper_NoStoreRegistered_DoesNotThrow()
    {
        var services = new ServiceCollection();
        services.AddAuditing().EnableBuffering().ApplyDecorators();

        var provider = services.BuildServiceProvider();
        provider.GetService<IAuditStore>().Should().BeNull();
    }

    [Fact]
    public void AddAuditing_RegistersGenericAuditLogger()
    {
        var services = new ServiceCollection();
        services.AddAuditing().UseStore<InMemoryAuditStore>();

        var provider = services.BuildServiceProvider();
        var logger = provider.GetService<IAuditLogger<DiRegistrationTests>>();
        logger.Should().NotBeNull().And.BeOfType<AuditLogger<DiRegistrationTests>>();
    }

    [Fact]
    public void AuditBuilderHelper_ConfigRegisteredAtFirstIndex_FindsIndexZero()
    {
        var services = new ServiceCollection();
        // AddAuditing is called first on empty collection, so its AuditConfiguration is registered at index 0 of services!
        var builder = services.AddAuditing(cfg => cfg.EnableIntegrityChain = true);
        services.AddSingleton<IAuditIntegrityProvider>(new TestAuditIntegrityProvider());
        services.AddSingleton<IAuditStore, InMemoryAuditStore>();

        builder.ApplyDecorators();

        var provider = services.BuildServiceProvider();
        var store = provider.GetRequiredService<IAuditStore>();
        store.Should().BeOfType<IntegrityAuditStoreDecorator>();
    }

    [Fact]
    public void AuditBuilderHelper_MultipleConfigs_BreaksAtLastConfig()
    {
        var services = new ServiceCollection();
        // Config at index 0 has false
        var builder = services.AddAuditing(cfg => cfg.EnableIntegrityChain = false);
        // Second config added at end has true
        services.AddSingleton(new AuditConfiguration { EnableIntegrityChain = true });
        services.AddSingleton<IAuditIntegrityProvider>(new TestAuditIntegrityProvider());
        services.AddSingleton<IAuditStore, InMemoryAuditStore>();

        builder.ApplyDecorators();

        var provider = services.BuildServiceProvider();
        var store = provider.GetRequiredService<IAuditStore>();
        store.Should().BeOfType<IntegrityAuditStoreDecorator>();
    }

    [Fact]
    public void AuditBuilderHelper_BufferedOptionsRegisteredAtFirstIndex_FindsIndexZero()
    {
        var services = new ServiceCollection();
        // Registered at index 0
        services.AddSingleton(new BufferedAuditStoreOptions { BatchSize = 42 });
        services.AddSingleton<IAuditStore, InMemoryAuditStore>();

        var builder = services.AddAuditing();
        builder.ApplyDecorators();

        var provider = services.BuildServiceProvider();
        var store = provider.GetRequiredService<IAuditStore>();
        store.Should().BeOfType<BufferedAuditStoreDecorator>();
    }

    [Fact]
    public void AuditBuilderHelper_MultipleBufferedOptions_BreaksAtLastOptions()
    {
        var services = new ServiceCollection();
        services.AddSingleton(new BufferedAuditStoreOptions { BatchSize = 10 });
        services.AddSingleton(new BufferedAuditStoreOptions { BatchSize = 50 });
        services.AddSingleton<IAuditStore, InMemoryAuditStore>();

        var builder = services.AddAuditing();
        builder.ApplyDecorators();

        var descriptor = services.First(d => d.ServiceType == typeof(IAuditStore));
        descriptor.ImplementationFactory.Should().NotBeNull();
    }

    [Fact]
    public void AuditBuilderHelper_MultipleStores_BreaksAtLastStore()
    {
        var services = new ServiceCollection();
        services.AddSingleton(new BufferedAuditStoreOptions());
        // Store 0:
        services.AddSingleton<IAuditStore>(new InMemoryAuditStore());
        // Store 1:
        services.AddSingleton<IAuditStore>(new InMemoryAuditStore());

        var builder = services.AddAuditing();
        builder.ApplyDecorators();

        // Store at index 2 (last) was decorated, Store at index 1 was not
        services[1].ImplementationInstance.Should().BeOfType<InMemoryAuditStore>();
        services[2].ImplementationFactory.Should().NotBeNull();
    }

    [Fact]
    public void AuditBuilderHelper_IntegrityChainEnabled_RegistersHashAlgorithmAndService()
    {
        var services = new ServiceCollection();
        var builder = services.AddAuditing(cfg => cfg.EnableIntegrityChain = true);
        services.AddSingleton<IAuditIntegrityProvider>(new TestAuditIntegrityProvider());
        services.AddSingleton<IAuditStore, InMemoryAuditStore>();

        builder.ApplyDecorators();

        var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IAuditHashAlgorithm>().Should().BeOfType<HmacSha256AuditHashAlgorithm>();
        provider.GetRequiredService<HmacAuditIntegrityService>().Should().NotBeNull();
    }

    [Fact]
    public void AuditBuilderHelper_AlreadyIntegrityDecoratorInstance_DoesNotWrapAgain()
    {
        var services = new ServiceCollection();
        var builder = services.AddAuditing(cfg => cfg.EnableIntegrityChain = true);
        services.AddSingleton<IAuditIntegrityProvider>(new TestAuditIntegrityProvider());

        var keyProvider = new TestAuditIntegrityProvider();
        var integrityService = new HmacAuditIntegrityService(keyProvider, new HmacSha256AuditHashAlgorithm());
        var alreadyDecorated = new IntegrityAuditStoreDecorator(new InMemoryAuditStore(), integrityService);
        services.AddSingleton<IAuditStore>(alreadyDecorated);

        builder.ApplyDecorators();

        var descriptor = services.First(d => d.ServiceType == typeof(IAuditStore));
        descriptor.ImplementationInstance.Should().BeSameAs(alreadyDecorated);
        descriptor.ImplementationFactory.Should().BeNull();
    }

    [Fact]
    public void AuditBuilderHelper_AlreadyIntegrityDecoratorFactory_DoesNotWrapAgain()
    {
        var services = new ServiceCollection();
        var builder = services.AddAuditing(cfg => cfg.EnableIntegrityChain = true);
        services.AddSingleton<IAuditIntegrityProvider>(new TestAuditIntegrityProvider());

        var keyProvider = new TestAuditIntegrityProvider();
        var integrityService = new HmacAuditIntegrityService(keyProvider, new HmacSha256AuditHashAlgorithm());
        var alreadyDecorated = new IntegrityAuditStoreDecorator(new InMemoryAuditStore(), integrityService);
        services.AddSingleton<IAuditStore>(sp => alreadyDecorated);

        builder.ApplyDecorators();

        var provider = services.BuildServiceProvider();
        var store = provider.GetRequiredService<IAuditStore>();
        store.Should().BeSameAs(alreadyDecorated);
    }

    [Fact]
    public void AuditBuilderHelper_AlreadyBufferedDecoratorInstance_DoesNotWrapAgain()
    {
        var services = new ServiceCollection();
        services.AddSingleton(new BufferedAuditStoreOptions());

        var buffered = new BufferedAuditStoreDecorator(new InMemoryAuditStore());
        services.AddSingleton<IAuditStore>(buffered);

        var builder = services.AddAuditing();
        builder.ApplyDecorators();

        var descriptor = services.First(d => d.ServiceType == typeof(IAuditStore));
        descriptor.ImplementationInstance.Should().BeSameAs(buffered);
        descriptor.ImplementationFactory.Should().BeNull();
    }

    [Fact]
    public void EnableBuffering_RemovesOptionsFromIndexZero()
    {
        var services = new ServiceCollection();
        // Put options at index 0
        services.AddSingleton(new BufferedAuditStoreOptions { BatchSize = 10 });
        services.AddSingleton<IAuditStore, InMemoryAuditStore>();

        var builder = services.AddAuditing();
        builder.EnableBuffering(opt => opt.BatchSize = 42);

        services.Count(s => s.ServiceType == typeof(BufferedAuditStoreOptions)).Should().Be(1);
    }

    [Fact]
    public void ApplyDecorators_InvokesAuditBuilderHelper()
    {
        var services = new ServiceCollection();
        var builder = services.AddAuditing(cfg => cfg.EnableIntegrityChain = true);
        services.AddSingleton<IAuditIntegrityProvider>(new TestAuditIntegrityProvider());
        services.AddSingleton<IAuditStore, InMemoryAuditStore>();

        builder.ApplyDecorators();

        var provider = services.BuildServiceProvider();
        var store = provider.GetRequiredService<IAuditStore>();
        store.Should().BeOfType<IntegrityAuditStoreDecorator>();
    }

    private sealed class CustomActorProviderStub : IAuditActorProvider
    {
        public AuditActor GetCurrentActor() => AuditActor.Anonymous;
    }
}



