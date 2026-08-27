// Copyright © Erickson Lopez. MIT License.
using System;
using System.Diagnostics;
using EricksonLopez.Auditing;

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

        activity.SetTag(AuditActivitySource.Tags.TenantId, record.Context.TenantId);
        activity.SetTag(AuditActivitySource.Tags.ActionCode, record.Action.Code);
        activity.SetTag(AuditActivitySource.Tags.ResourceType, record.Resource.Type);
        activity.SetTag(AuditActivitySource.Tags.ResourceId, record.Resource.Id);
        activity.SetTag(AuditActivitySource.Tags.ActorId, record.Actor.Id);
        activity.SetTag(AuditActivitySource.Tags.ActorType, record.Actor.Type.ToString());
        activity.SetTag(AuditActivitySource.Tags.Outcome, record.Outcome.ToString());
        activity.SetTag(AuditActivitySource.Tags.RecordId, record.Id.ToString());
    }
}
