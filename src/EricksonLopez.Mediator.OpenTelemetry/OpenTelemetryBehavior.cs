// Copyright © Erickson Lopez. MIT License.
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using EricksonLopez.Mediator;

namespace EricksonLopez.Mediator.OpenTelemetry;

/// <summary>
/// Represents a pipeline behavior that generates OpenTelemetry distributed traces and records request metrics.
/// </summary>
/// <remarks>
/// Combines distributed tracing via <see cref="ActivitySource"/> with metrics via <see cref="System.Diagnostics.Metrics.Meter"/>.
/// Operates with zero overhead when no trace or metric listeners are attached.
/// </remarks>
/// <typeparam name="TRequest">The type of request being traced.</typeparam>
/// <typeparam name="TResponse">The type of response returned by the pipeline.</typeparam>
public sealed class OpenTelemetryBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
{
    internal static readonly ActivitySource DefaultActivitySource = new("EricksonLopez.Mediator");
    private readonly ActivitySource _activitySource;
    private readonly Action<Activity, object>? _enrichActivity;

    // Cache type names to avoid reflection overhead on every request (BL-009 / ADR-030)
    private static readonly string RequestName = typeof(TRequest).Name;
    private static readonly string RequestFullName = typeof(TRequest).FullName!;
    private static readonly string ResponseFullName = typeof(TResponse).FullName!;

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenTelemetryBehavior{TRequest, TResponse}"/> class with default options.
    /// </summary>
    public OpenTelemetryBehavior()
    {
        _activitySource = DefaultActivitySource;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenTelemetryBehavior{TRequest, TResponse}"/> class with the specified options.
    /// </summary>
    /// <param name="options">
    /// The configuration options for OpenTelemetry instrumentation.
    /// When <see langword="null"/>, the default activity source name and no enrichment callback are used.
    /// </param>
    public OpenTelemetryBehavior(MediatorOpenTelemetryOptions? options = null)
    {
        _activitySource = string.IsNullOrEmpty(options?.ActivitySourceName) || options!.ActivitySourceName == "EricksonLopez.Mediator"
            ? DefaultActivitySource
            : new ActivitySource(options.ActivitySourceName);
        _enrichActivity = options?.EnrichActivity;
    }

    /// <inheritdoc/>
    public async ValueTask<TResponse> Handle<TNext>(TRequest request, TNext next, CancellationToken cancellationToken)
        where TNext : struct, INext<TResponse>
    {
        var startTime = Stopwatch.GetTimestamp();

        using var activity = _activitySource.StartActivity($"Mediator {RequestName}", ActivityKind.Internal);

        activity?.SetTag("mediator.request.name", RequestName);
        activity?.SetTag("mediator.request.type", RequestFullName);
        activity?.SetTag("mediator.response.type", ResponseFullName);

        if (activity != null && _enrichActivity != null && request != null)
        {
            _enrichActivity(activity, request);
        }

        try
        {
            var response = await next.InvokeAsync().ConfigureAwait(false);
            activity?.SetStatus(ActivityStatusCode.Ok);

            var durationMs = Stopwatch.GetElapsedTime(startTime).TotalMilliseconds;
            MediatorMetrics.RecordRequest(RequestName, durationMs);

            return response;
        }
        catch (Exception ex)
        {
            MediatorMetrics.RecordFailure(RequestName);

            if (activity != null)
            {
                activity.SetStatus(ActivityStatusCode.Error, ex.Message);
                activity.AddException(ex);
            }
            throw;
        }
    }
}
