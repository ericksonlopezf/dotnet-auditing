// Copyright © Erickson Lopez. MIT License.
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using EricksonLopez.Mediator;
using EricksonLopez.Mediator.HealthChecks;
using EricksonLopez.Mediator.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using NSubstitute;
using Xunit;

namespace EricksonLopez.Mediator.Tests;

public class MediatorHealthCheckTests
{
    [Fact]
    public async Task CheckHealthAsync_WithSender_ReturnsHealthyStatus()
    {
        var sender = new FakeMediator();
        var healthCheck = new MediatorHealthCheck(sender);
        var context = new HealthCheckContext();

        var result = await healthCheck.CheckHealthAsync(context, CancellationToken.None);

        result.Status.Should().Be(HealthStatus.Healthy);
        result.Description.Should().Be("Mediator pipeline and dispatch engine are active and ready.");
    }

    [Fact]
    public async Task CheckHealthAsync_WithoutSender_RespectsContextFailureStatus()
    {
        var healthCheck = new MediatorHealthCheck(sender: null);
        var registration = new HealthCheckRegistration("mediator", healthCheck, HealthStatus.Unhealthy, null);
        var context = new HealthCheckContext { Registration = registration };

        var result = await healthCheck.CheckHealthAsync(context, CancellationToken.None);

        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Description.Should().Be("Mediator sender is not registered in the service provider.");
    }

    [Fact]
    public async Task CheckHealthAsync_WithoutSenderAndNullRegistration_ReturnsDegradedStatus()
    {
        var healthCheck = new MediatorHealthCheck(sender: null);
        var context = new HealthCheckContext { Registration = null! };

        var result = await healthCheck.CheckHealthAsync(context, CancellationToken.None);

        result.Status.Should().Be(HealthStatus.Degraded);
        result.Description.Should().Be("Mediator sender is not registered in the service provider.");
    }

    [Fact]
    public async Task CheckHealthAsync_WithCustomHealthCheckRegistration_ReturnsHealthyStatus()
    {
        var sender = new FakeMediator();
        var healthCheck = new MediatorHealthCheck(sender);
        var registration = new HealthCheckRegistration("mediator-check", healthCheck, HealthStatus.Unhealthy, new[] { "ready", "live" });
        var context = new HealthCheckContext { Registration = registration };

        var result = await healthCheck.CheckHealthAsync(context, CancellationToken.None);

        result.Status.Should().Be(HealthStatus.Healthy);
        result.Description.Should().Be("Mediator pipeline and dispatch engine are active and ready.");
    }

    [Fact]
    public async Task CheckHealthAsync_WithCancellationToken_CompletesSuccessfully()
    {
        var sender = new FakeMediator();
        var healthCheck = new MediatorHealthCheck(sender);
        using var cts = new CancellationTokenSource();

        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext(), cts.Token);

        result.Status.Should().Be(HealthStatus.Healthy);
    }

    [Fact]
    public void AddMediatorHealthCheck_ValidServiceCollection_RegistersTransientHealthCheck()
    {
        var services = new ServiceCollection();
        services.AddMediatorHealthCheck();

        var descriptor = services.FirstOrDefault(s =>
            s.ServiceType == typeof(IHealthCheck) &&
            s.ImplementationType == typeof(MediatorHealthCheck));

        descriptor.Should().NotBeNull();
        descriptor!.Lifetime.Should().Be(ServiceLifetime.Transient);
    }

    [Fact]
    public void AddMediatorHealthCheck_NullServiceCollection_ThrowsArgumentNullException()
    {
        IServiceCollection services = null!;
        var act = () => services.AddMediatorHealthCheck();

        act.Should().Throw<ArgumentNullException>().WithParameterName("services");
    }
}
