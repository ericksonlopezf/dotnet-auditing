// Copyright © Erickson Lopez. MIT License.
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace EricksonLopez.Auditing.MySql;

[JsonSerializable(typeof(List<AuditChangeDto>))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[ExcludeFromCodeCoverage]
internal sealed partial class AuditJsonContext : JsonSerializerContext { }
