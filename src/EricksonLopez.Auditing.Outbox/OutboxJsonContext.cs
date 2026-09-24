// Copyright © Erickson Lopez. MIT License.
using System.Collections.Generic;
using System.Text.Json.Serialization;
using EricksonLopez.Auditing;

namespace EricksonLopez.Auditing.Outbox;

/// <summary>Source-generated JSON context for AOT compliance in Outbox serialization.</summary>
[JsonSerializable(typeof(AuditRecord))]
[JsonSerializable(typeof(IReadOnlyList<AuditRecord>))]
internal sealed partial class OutboxJsonContext : JsonSerializerContext
{
}
