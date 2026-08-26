// Copyright © Erickson Lopez. MIT License.
using System.Diagnostics.Metrics;

namespace EricksonLopez.Auditing.OpenTelemetry;

/// <summary>Provides OpenTelemetry metrics instruments for measuring auditing operations.</summary>
public static class AuditMetrics
{
    /// <summary>Gets the canonical meter name for auditing metrics.</summary>
    public const string MeterName = "EricksonLopez.Auditing";

    private static readonly Meter Meter = new(MeterName, "1.0.0");

    /// <summary>Gets the counter instrument tracking successfully persisted audit records.</summary>
    public static readonly Counter<long> RecordsAppended = Meter.CreateCounter<long>(
        "audit.records_appended",
        description: "Number of audit records successfully persisted.");

    /// <summary>Gets the counter instrument tracking executed audit record queries.</summary>
    public static readonly Counter<long> QueriesExecuted = Meter.CreateCounter<long>(
        "audit.queries_executed",
        description: "Number of audit query operations executed.");

    /// <summary>Gets the counter instrument tracking cryptographic audit chain integrity verifications.</summary>
    public static readonly Counter<long> IntegrityVerifications = Meter.CreateCounter<long>(
        "audit.integrity_verifications",
        description: "Number of cryptographic audit integrity chain verifications performed.");
}
