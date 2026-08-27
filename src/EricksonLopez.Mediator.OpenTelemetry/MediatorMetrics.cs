// Copyright © Erickson Lopez. MIT License.
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace EricksonLopez.Mediator.OpenTelemetry;

/// <summary>
/// Provides OpenTelemetry metrics instrumentation counters and histograms for mediator operations.
/// </summary>
public static class MediatorMetrics
{
    private static readonly Meter Meter = new("EricksonLopez.Mediator", "1.0.0");

    /// <summary>Total number of requests dispatched (commands + queries).</summary>
    private static readonly Counter<long> RequestCount =
        Meter.CreateCounter<long>("mediator.requests.total", "requests",
            "Total number of requests dispatched via IMediator.Send.");

    /// <summary>Total number of notifications published.</summary>
    private static readonly Counter<long> NotificationCount =
        Meter.CreateCounter<long>("mediator.notifications.total", "notifications",
            "Total number of notifications dispatched via IMediator.Publish.");

    /// <summary>Total number of failed requests (unhandled exception).</summary>
    private static readonly Counter<long> FailureCount =
        Meter.CreateCounter<long>("mediator.requests.failures", "requests",
            "Total number of requests that resulted in an unhandled exception.");

    /// <summary>
    /// Histogram of request durations in milliseconds.
    /// </summary>
    private static readonly Histogram<double> RequestDuration =
        Meter.CreateHistogram<double>("mediator.request.duration", "ms",
            "Duration of each request dispatched via IMediator.Send.");

    /// <summary>
    /// Records a successful request dispatch.
    /// </summary>
    internal static void RecordRequest(string requestType, double durationMs)
    {
        var tags = new TagList { { "request.type", requestType } };
        RequestCount.Add(1, tags);
        RequestDuration.Record(durationMs, tags);
    }

    /// <summary>
    /// Records a failed request dispatch.
    /// </summary>
    internal static void RecordFailure(string requestType)
    {
        var tags = new TagList { { "request.type", requestType } };
        FailureCount.Add(1, tags);
    }

    /// <summary>
    /// Records a notification publish.
    /// </summary>
    internal static void RecordNotification(string notificationType)
    {
        NotificationCount.Add(1, new TagList { { "notification.type", notificationType } });
    }
}
