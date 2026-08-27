// Copyright © Erickson Lopez. MIT License.
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using EricksonLopez.Auditing.Testing;

namespace EricksonLopez.Auditing.Showcase.Levels;

/// <summary>
/// Custom Domain Action Definitions (AuditAction is extensible, not a closed enum)
/// </summary>
public static class CustomDomainActions
{
    public static readonly AuditAction ProcessPayment = new("ProcessPayment");
    public static readonly AuditAction ExportGdprData = new("ExportGdprData");
    public static readonly AuditAction AuthorizeRefund = new("AuthorizeRefund");
    public static readonly AuditAction BulkUserImport = new("BulkUserImport");
}

/// <summary>
/// Level 3 — Real-World Use Cases: Enterprise Business Scenarios Derived from Public API
/// Covers predefined AuditActions and full enterprise business patterns.
/// </summary>
public static class Level03_RealWorldUseCases
{
    public static async Task RunAsync()
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("\n================================================================================");
        Console.WriteLine(" [LEVEL 3] — REAL-WORLD ENTERPRISE USE CASES");
        Console.WriteLine("================================================================================\n");
        Console.ResetColor();

        var services = new ServiceCollection();
        services.AddAuditing()
                .UseStore<InMemoryAuditStore>();

        var sp = services.BuildServiceProvider();
        var store = sp.GetRequiredService<IAuditStore>();

        // ── Scenario 1: Authentication & Privilege Elevation ─────────────────────
        Console.WriteLine("── Scenario 1: Authentication and Permission Granting ──");
        var loginRecord = new AuditRecord
        {
            Id = AuditId.NewId(),
            OccurredAt = DateTimeOffset.UtcNow,
            Actor = new AuditActor(AuditActorType.User, "usr-bob-102", "bob.admin@domain.com"),
            Action = AuditAction.Login,
            Resource = new AuditResource("Session", "sess-991823"),
            Outcome = AuditOutcome.Success,
            Context = new AuditContext(
                TenantId: "tenant-fintech",
                Source: "IdentityService",
                IpAddress: "192.168.1.100",
                UserAgent: "Mozilla/5.0 (Windows NT 10.0; Win64; x64)")
        };
        await store.AppendAsync(loginRecord);
        Console.WriteLine($"✓ [LOGIN] User '{loginRecord.Actor.DisplayName}' logged in.");

        var grantPermRecord = new AuditRecord
        {
            Id = AuditId.NewId(),
            OccurredAt = DateTimeOffset.UtcNow,
            Actor = new AuditActor(AuditActorType.User, "usr-bob-102", "bob.admin@domain.com"),
            Action = AuditAction.GrantPermission,
            Resource = new AuditResource("RoleAssignment", "role-financial-officer"),
            Outcome = AuditOutcome.Success,
            Context = new AuditContext(
                TenantId: "tenant-fintech",
                Source: "AdminPortal",
                CorrelationId: "corr-perm-grant-01"),
            Changes = new[]
            {
                new AuditChange("TargetUser", null, "usr-alice-404"),
                new AuditChange("AssignedRole", null, "FinancialOfficer")
            }
        };
        await store.AppendAsync(grantPermRecord);
        Console.WriteLine($"✓ [GRANT_PERMISSION] Role 'FinancialOfficer' granted to 'usr-alice-404'.\n");

        // ── Scenario 2: Modification within Aggregate Hierarchy ─────────────────
        Console.WriteLine("── Scenario 2: Sub-entity within Aggregate Root ──");
        var orderItemRecord = new AuditRecord
        {
            Id = AuditId.NewId(),
            OccurredAt = DateTimeOffset.UtcNow,
            Actor = new AuditActor(AuditActorType.User, "usr-alice-404", "alice@domain.com"),
            Action = AuditAction.Update,
            Resource = new AuditResource(
                Type: "OrderItem",
                Id: "item-line-3",
                AggregateType: "Order",
                AggregateId: "order-2026-9901"),
            Outcome = AuditOutcome.Success,
            Context = new AuditContext(
                TenantId: "tenant-fintech",
                Source: "OrderManagementService"),
            Changes = new[]
            {
                new AuditChange("Quantity", "2", "5"),
                new AuditChange("UnitPrice", "49.99", "45.00"),
                AuditChange.Redacted("DiscountCouponSecret") // Explicitly redacted field
            }
        };
        await store.AppendAsync(orderItemRecord);
        Console.WriteLine($"✓ [UPDATE] Item '{orderItemRecord.Resource.Id}' in Aggregate Root Order '{orderItemRecord.Resource.AggregateId}'.");
        Console.WriteLine($"  - Changes recorded with secret redaction.\n");

        // ── Scenario 3: Custom Action and Denial ─────────────────────────────────
        Console.WriteLine("── Scenario 3: Custom Action & Access Denial ──");
        var deniedRecord = new AuditRecord
        {
            Id = AuditId.NewId(),
            OccurredAt = DateTimeOffset.UtcNow,
            Actor = new AuditActor(AuditActorType.User, "usr-mallory-666", "mallory@external.com"),
            Action = CustomDomainActions.AuthorizeRefund,
            Resource = new AuditResource("RefundRequest", "ref-5501"),
            Outcome = AuditOutcome.Denied,
            ErrorCode = "ERR_INSUFFICIENT_SECURITY_CLEARANCE",
            Context = new AuditContext(
                TenantId: "tenant-fintech",
                Source: "PaymentGatewayService")
        };
        await store.AppendAsync(deniedRecord);
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"⚠ [DENIED] Custom action '{deniedRecord.Action.Code}' denied with ErrorCode: '{deniedRecord.ErrorCode}'.");
        Console.ResetColor();
        Console.WriteLine();

        // ── Scenario 4: Predefined Lifecycle Actions ────────────────────────────
        Console.WriteLine("── Scenario 4: Predefined Lifecycle Actions — Download, Send, Cancel, Restore ──");

        // AuditAction.Download — download protected resource
        var downloadRecord = new AuditRecord
        {
            Id = AuditId.NewId(),
            OccurredAt = DateTimeOffset.UtcNow,
            Actor = new AuditActor(AuditActorType.User, "usr-alice-404", "alice@domain.com"),
            Action = AuditAction.Download,
            Resource = new AuditResource("FinancialReport", "report-q1-2026.pdf"),
            Outcome = AuditOutcome.Success,
            Context = new AuditContext("tenant-fintech", "CompliancePortal")
        };
        await store.AppendAsync(downloadRecord);
        Console.WriteLine($"✓ [DOWNLOAD] Financial report downloaded: '{downloadRecord.Resource.Id}'.");

        // AuditAction.Send — send notification or document
        var sendRecord = new AuditRecord
        {
            Id = AuditId.NewId(),
            OccurredAt = DateTimeOffset.UtcNow,
            Actor = new AuditActor(AuditActorType.Service, "notification-svc", "Notification Service"),
            Action = AuditAction.Send,
            Resource = new AuditResource("EmailNotification", "notif-8812"),
            Outcome = AuditOutcome.Success,
            Context = new AuditContext(
                TenantId: "tenant-fintech",
                Source: "NotificationWorker",
                CorrelationId: "corr-invoice-notify-01")
        };
        await store.AppendAsync(sendRecord);
        Console.WriteLine($"✓ [SEND] Notification '{sendRecord.Resource.Id}' sent by {sendRecord.Actor.DisplayName}.");

        // AuditAction.Cancel — cancellation of a pending operation
        var cancelRecord = new AuditRecord
        {
            Id = AuditId.NewId(),
            OccurredAt = DateTimeOffset.UtcNow,
            Actor = new AuditActor(AuditActorType.User, "usr-alice-404", "alice@domain.com"),
            Action = AuditAction.Cancel,
            Resource = new AuditResource("BankTransfer", "transfer-449922"),
            Outcome = AuditOutcome.Success,
            Context = new AuditContext(
                TenantId: "tenant-fintech",
                Source: "BankingService",
                CausationId: "approve-transfer-cmd-449922"),
            Changes = new[]
            {
                new AuditChange("Status", "Pending", "Cancelled")
            }
        };
        await store.AppendAsync(cancelRecord);
        Console.WriteLine($"✓ [CANCEL] Transfer '{cancelRecord.Resource.Id}' cancelled. Status: Pending → Cancelled.");

        // AuditAction.Restore — restoration from soft delete
        var restoreRecord = new AuditRecord
        {
            Id = AuditId.NewId(),
            OccurredAt = DateTimeOffset.UtcNow,
            Actor = new AuditActor(AuditActorType.User, "usr-bob-102", "bob.admin@domain.com"),
            Action = AuditAction.Restore,
            Resource = new AuditResource("CustomerAccount", "acc-deleted-0041"),
            Outcome = AuditOutcome.Success,
            Context = new AuditContext(
                TenantId: "tenant-fintech",
                Source: "AdminPortal"),
            Changes = new[]
            {
                new AuditChange("IsDeleted", "true", "false"),
                new AuditChange("DeletedAt", "2026-01-15", null)
            }
        };
        await store.AppendAsync(restoreRecord);
        Console.WriteLine($"✓ [RESTORE] Account '{restoreRecord.Resource.Id}' restored from soft delete.");

        // ── Scenario 5: Platform Event via SystemTenantId ────────────────────────
        Console.WriteLine("\n── Scenario 5: Platform Event (AuditContext.SystemTenantId) ──");
        var platformEvent = new AuditRecord
        {
            Id = AuditId.NewId(),
            OccurredAt = DateTimeOffset.UtcNow,
            Actor = AuditActor.System,
            Action = new AuditAction("CleanupExpiredSessions"),
            Resource = new AuditResource("SessionStore", "global"),
            Outcome = AuditOutcome.Success,
            Context = new AuditContext(
                TenantId: AuditContext.SystemTenantId,
                Source: "BackgroundMaintenanceWorker")
        };
        await store.AppendAsync(platformEvent);
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"✓ [SYSTEM] Platform event with TenantId='{AuditContext.SystemTenantId}' registered.");
        Console.ResetColor();
        Console.WriteLine("  (Does not belong to any customer tenant — infrastructure scope).\n");

        // ── Summary of Records for Tenant ────────────────────────────────────────
        var allRecords = await store.QueryAsync(new AuditQuery { TenantId = "tenant-fintech" });
        Console.WriteLine($"✓ Total accumulated audit events for 'tenant-fintech': {allRecords.Records.Count}");
    }
}
