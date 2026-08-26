// Copyright © Erickson Lopez. MIT License.
using System;
using EricksonLopez.Auditing.Testing;

namespace EricksonLopez.Auditing.IntegrationTests;

internal static class Builders
{
    public static AuditRecord Build(
        string tenantId = "tenant-a",
        string actorId = "user-42",
        string resourceType = "Order",
        string resourceId = "order-1",
        AuditOutcome outcome = AuditOutcome.Success,
        string? correlationId = null)
    {
        return AuditRecordBuilder.BuildDefault(tenantId, actorId, resourceType, resourceId, outcome, correlationId);
    }
}
