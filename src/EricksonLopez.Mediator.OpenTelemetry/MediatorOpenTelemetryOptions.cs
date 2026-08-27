// Copyright © Erickson Lopez. MIT License.
using System;
using System.Diagnostics;

namespace EricksonLopez.Mediator.OpenTelemetry;

/// <summary>
/// Specifies configuration options for OpenTelemetry activity and metric instrumentation in the mediator pipeline.
/// </summary>
public sealed class MediatorOpenTelemetryOptions
{
    /// <summary>
    /// Gets or sets the name of the <see cref="ActivitySource"/> used for distributed tracing.
    /// </summary>
    public string ActivitySourceName { get; set; } = "EricksonLopez.Mediator";

    /// <summary>
    /// Gets or sets a callback delegate used to enrich created <see cref="Activity"/> instances with custom tags.
    /// </summary>
    public Action<Activity, object>? EnrichActivity { get; set; }
}
