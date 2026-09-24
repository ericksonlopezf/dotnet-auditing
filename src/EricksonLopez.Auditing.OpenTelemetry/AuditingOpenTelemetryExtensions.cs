// Copyright © Erickson Lopez. MIT License.
using System;
using System.Diagnostics;
using EricksonLopez.Auditing;
using Microsoft.Extensions.DependencyInjection;

namespace EricksonLopez.Auditing.OpenTelemetry;

/// <summary>Provides extension methods for enriching OpenTelemetry activities with audit metadata.</summary>
public static class AuditingOpenTelemetryExtensions
{
    /// <summary>Enriches the current ambient <see cref="Activity"/> with semantic tags from the specified audit record.</summary>
    /// <param name="record">The audit record containing context to populate onto the active activity.</param>
    public static void EnrichCurrentActivity(this AuditRecord record)
    {
        if (record is null)
            return;

        var activity = Activity.Current;
        if (activity is null)
            return;

        activity.SetTag(AuditActivitySource.Tags.TenantId, record.Context.TenantId.Value);
        activity.SetTag(AuditActivitySource.Tags.ActionCode, record.Action.Code);
        activity.SetTag(AuditActivitySource.Tags.ResourceType, record.Resource.Type);
        activity.SetTag(AuditActivitySource.Tags.ResourceId, record.Resource.Id);
        activity.SetTag(AuditActivitySource.Tags.ActorId, record.Actor.Id);
        activity.SetTag(AuditActivitySource.Tags.ActorType, record.Actor.Type.ToString());
        activity.SetTag(AuditActivitySource.Tags.Outcome, record.Outcome.ToString());
        activity.SetTag(AuditActivitySource.Tags.RecordId, record.Id.ToString());
    }

    /// <summary>
    /// Adds OpenTelemetry metrics and tracing instrumentation decorators for registered audit store and integrity verifier services.
    /// </summary>
    /// <param name="builder">The audit builder being configured.</param>
    /// <returns>The audit builder instance for method chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> is <see langword="null"/></exception>
    public static IAuditBuilder AddOpenTelemetryInstrumentation(this IAuditBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        for (int i = builder.Services.Count - 1; i >= 0; i--)
        {
            var descriptor = builder.Services[i];
            if (descriptor.ServiceType == typeof(IAuditStore))
            {
                builder.Services.RemoveAt(i);
                if (descriptor.ImplementationInstance is IAuditStore instance)
                {
                    builder.Services.AddSingleton<IAuditStore>(new OpenTelemetryAuditStoreDecorator(instance));
                }
                else if (descriptor.ImplementationFactory != null)
                {
                    builder.Services.AddSingleton<IAuditStore>(sp =>
                        new OpenTelemetryAuditStoreDecorator((IAuditStore)descriptor.ImplementationFactory(sp)));
                }
                else if (descriptor.ImplementationType != null)
                {
                    var implType = descriptor.ImplementationType;
                    builder.Services.AddSingleton(implType);
                    builder.Services.AddSingleton<IAuditStore>(sp =>
                        new OpenTelemetryAuditStoreDecorator((IAuditStore)sp.GetRequiredService(implType)));
                }
                break;
            }
        }

        return builder;
    }
}

