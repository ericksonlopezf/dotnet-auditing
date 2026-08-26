// Copyright © Erickson Lopez. MIT License.
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EricksonLopez.Auditing.UnitTests;

[JsonSerializable(typeof(List<FakeChangeDto>))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[ExcludeFromCodeCoverage]
internal sealed partial class FakeDbJsonContext : JsonSerializerContext { }

internal sealed record FakeChangeDto(string Field, string? OldValue, string? NewValue, bool IsRedacted);

[ExcludeFromCodeCoverage]
internal static class FakeDbDataReaderFactory
{
    public static readonly string[] StandardColumns = new[]
    {
        "Id", "OccurredAt", "TenantId", "Source", "ActorType", "ActorId", "ActorName",
        "ActionCode", "ResourceType", "ResourceId", "AggregateType", "AggregateId",
        "Outcome", "ErrorCode", "CorrelationId", "CausationId", "RequestId", "IpAddress", "UserAgent",
        "ChangesJson", "IntegrityHash", "PreviousHash"
    };

    public static readonly string[] OracleColumns = new[]
    {
        "ID", "OCCURRED_AT", "TENANT_ID", "SOURCE", "ACTOR_TYPE", "ACTOR_ID", "ACTOR_NAME",
        "ACTION_CODE", "RESOURCE_TYPE", "RESOURCE_ID", "AGGREGATE_TYPE", "AGGREGATE_ID",
        "OUTCOME", "ERROR_CODE", "CORRELATION_ID", "CAUSATION_ID", "REQUEST_ID", "IP_ADDRESS", "USER_AGENT",
        "CHANGES_JSON", "INTEGRITY_HASH", "PREVIOUS_HASH"
    };

    public static FakeDbDataReader Create(params AuditRecord[] records) => Create((IEnumerable<AuditRecord>)records);

    public static FakeDbDataReader Create(IEnumerable<AuditRecord> records, bool isStringId = false, bool isStringDate = false)
    {
        var rows = new List<object?[]>();
        foreach (var r in records)
        {
            object idVal = isStringId ? r.Id.ToString("D") : r.Id;
            object dateVal = isStringDate ? r.OccurredAt.ToString("O") : r.OccurredAt.UtcDateTime;
            string? changesJson = SerializeChanges(r.Changes);

            rows.Add(new object?[]
            {
                idVal,
                dateVal,
                r.Context.TenantId,
                r.Context.Source,
                (byte)r.Actor.Type,
                r.Actor.Id,
                r.Actor.DisplayName,
                r.Action.Code,
                r.Resource.Type,
                r.Resource.Id,
                r.Resource.AggregateType,
                r.Resource.AggregateId,
                (byte)r.Outcome,
                r.ErrorCode,
                r.Context.CorrelationId,
                r.Context.CausationId,
                r.Context.RequestId,
                r.Context.IpAddress,
                r.Context.UserAgent,
                changesJson,
                r.IntegrityHash,
                r.PreviousHash
            });
        }
        return new FakeDbDataReader(StandardColumns, rows);
    }

    public static FakeDbDataReader CreateOracle(params AuditRecord[] records) => CreateOracle((IEnumerable<AuditRecord>)records);

    public static FakeDbDataReader CreateOracle(IEnumerable<AuditRecord> records)
    {
        var rows = new List<object?[]>();
        foreach (var r in records)
        {
            string? changesJson = SerializeChanges(r.Changes);

            rows.Add(new object?[]
            {
                r.Id.ToString("D"),
                r.OccurredAt.UtcDateTime,
                r.Context.TenantId,
                r.Context.Source,
                (byte)r.Actor.Type,
                r.Actor.Id,
                r.Actor.DisplayName,
                r.Action.Code,
                r.Resource.Type,
                r.Resource.Id,
                r.Resource.AggregateType,
                r.Resource.AggregateId,
                (byte)r.Outcome,
                r.ErrorCode,
                r.Context.CorrelationId,
                r.Context.CausationId,
                r.Context.RequestId,
                r.Context.IpAddress,
                r.Context.UserAgent,
                changesJson,
                r.IntegrityHash,
                r.PreviousHash
            });
        }
        return new FakeDbDataReader(OracleColumns, rows);
    }

    public static FakeDbDataReader CreateRaw(string[] columns, List<object?[]> rows)
    {
        return new FakeDbDataReader(columns, rows);
    }

    private static string? SerializeChanges(IReadOnlyList<AuditChange>? changes)
    {
        if (changes == null || changes.Count == 0) return null;

        var dtos = new List<FakeChangeDto>(changes.Count);
        foreach (var c in changes)
        {
            dtos.Add(new FakeChangeDto(c.Field, c.OldValue, c.NewValue, c.IsRedacted));
        }
        return JsonSerializer.Serialize(dtos, FakeDbJsonContext.Default.ListFakeChangeDto);
    }
}
