// Copyright © Erickson Lopez. MIT License.
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace EricksonLopez.Mediator.HealthChecks;

/// <summary>
/// Provides health check verification for the mediator dispatch pipeline and service registration.
/// </summary>
public sealed class MediatorHealthCheck : IHealthCheck
{
    private readonly ISender? _sender;

    /// <summary>
    /// Initializes a new instance of the <see cref="MediatorHealthCheck"/> class.
    /// </summary>
    /// <param name="sender">The optional sender service used to verify mediator registration.</param>
    public MediatorHealthCheck(ISender? sender = null)
    {
        _sender = sender;
    }

    /// <inheritdoc/>
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (_sender is null)
        {
            var status = context.Registration?.FailureStatus ?? HealthStatus.Degraded;
            return Task.FromResult(new HealthCheckResult(status, "Mediator sender is not registered in the service provider."));
        }

        return Task.FromResult(HealthCheckResult.Healthy("Mediator pipeline and dispatch engine are active and ready."));
    }
}
