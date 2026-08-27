// Copyright © Erickson Lopez. MIT License.

namespace EricksonLopez.Auditing.Dapper;

internal sealed record AuditChangeDto(
    string Field,
    string? OldValue,
    string? NewValue,
    bool IsRedacted);
