// Copyright © Erickson Lopez. MIT License.
using System.Diagnostics.Metrics;

namespace EricksonLopez.Auditing.OpenTelemetry;

/// <summary>Provides OpenTelemetry metrics instruments for measuring auditing operations.</summary>
public static class AuditMetrics
{
    /// <summary>Gets the canonical meter name for auditing metrics.</summary>
    public const string MeterName = "EricksonLopez.Auditing";

    private static readonly Meter _meter = new(MeterName, "2.0.0");

    /// <summary>Gets the counter instrument tracking successfully persisted audit records.</summary>
    public static readonly Counter<long> RecordsAppended = _meter.CreateCounter<long>(
        "audit.records_appended",
        description: "Number of audit records successfully persisted.");

    /// <summary>Gets the counter instrument tracking failed audit record persist operations.</summary>
    public static readonly Counter<long> RecordsFailed = _meter.CreateCounter<long>(
        "audit.records_failed",
        description: "Number of audit record persist operations that failed.");

    /// <summary>Gets the histogram instrument tracking append duration in milliseconds.</summary>
    public static readonly Histogram<double> AppendDuration = _meter.CreateHistogram<double>(
        "audit.append.duration_ms",
        unit: "ms",
        description: "Duration of audit append operations in milliseconds.");

    /// <summary>Gets the counter instrument tracking executed audit record queries.</summary>
    public static readonly Counter<long> QueriesExecuted = _meter.CreateCounter<long>(
        "audit.queries_executed",
        description: "Number of audit query operations executed.");

    /// <summary>Gets the histogram instrument tracking query duration in milliseconds.</summary>
    public static readonly Histogram<double> QueryDuration = _meter.CreateHistogram<double>(
        "audit.query.duration_ms",
        unit: "ms",
        description: "Duration of audit query operations in milliseconds.");

    /// <summary>Gets the counter instrument tracking cryptographic audit chain integrity verifications.</summary>
    public static readonly Counter<long> IntegrityVerifications = _meter.CreateCounter<long>(
        "audit.integrity_verifications",
        description: "Number of cryptographic audit integrity chain verifications performed.");
}
