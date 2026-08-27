// Copyright © Erickson Lopez. MIT License.
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using EricksonLopez.Auditing.Testing;

namespace EricksonLopez.Auditing.Showcase.Levels;

/// <summary>
/// Level 1 — Quick Start: Minimal Setup, DI Registration & First Audit Record
/// </summary>
public static class Level01_QuickStart
{
    public static async Task RunAsync()
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("\n================================================================================");
        Console.WriteLine(" [LEVEL 1] — QUICK START: MINIMAL SETUP & FIRST AUDIT RECORD");
        Console.WriteLine("================================================================================\n");
        Console.ResetColor();

        // ── 1. Dependency Injection Setup ────────────────────────────────────────
        var services = new ServiceCollection();
        services.AddAuditing()
                .UseStore<InMemoryAuditStore>();

        var serviceProvider = services.BuildServiceProvider();
        var auditStore = serviceProvider.GetRequiredService<IAuditStore>();

        Console.WriteLine("✓ Auditing services registered in IServiceCollection.");
        Console.WriteLine("✓ Configured store: InMemoryAuditStore (for testing / fast development).\n");

        // ── 2. Construct Canonical AuditRecord ────────────────────────────────────
        var correlationId = Guid.NewGuid().ToString("N");
        var record = new AuditRecord
        {
            Id = AuditId.NewId(),                          // UUIDv7 chronological generation
            OccurredAt = DateTimeOffset.UtcNow,
            Actor = new AuditActor(AuditActorType.User, "usr-9872", "alice@example.com"),
            Action = AuditAction.Create,
            Resource = new AuditResource("CustomerAccount", "acc-5541"),
            Outcome = AuditOutcome.Success,
            Context = new AuditContext(
                TenantId: "tenant-acme",
                Source: "CustomerOnboardingService",
                CorrelationId: correlationId,
                CausationId: "cmd-create-account-99",     // Originating command
                RequestId: "req-http-8812",               // Transport request ID
                IpAddress: "10.0.0.55",
                UserAgent: "Mozilla/5.0 (Windows NT 10.0)"),
            Changes = new[]
            {
                new AuditChange("AccountName", null, "Acme Corp"),
                new AuditChange("Tier", null, "Enterprise")
            }
        };

        // ── 3. Persist to Store ───────────────────────────────────────────────────
        await auditStore.AppendAsync(record);
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"✓ Record persisted with UUIDv7 ID: {record.Id}");
        Console.ResetColor();
        Console.WriteLine($"  • Actor: {record.Actor.DisplayName} ({record.Actor.Id}) [{record.Actor.Type}]");
        Console.WriteLine($"  • Action: {record.Action.Code}");
        Console.WriteLine($"  • Resource: {record.Resource.Type}:{record.Resource.Id}");
        Console.WriteLine($"  • Outcome: {record.Outcome}");
        Console.WriteLine($"  • Tenant: {record.Context.TenantId}");
        Console.WriteLine($"  • CorrelationId: {record.Context.CorrelationId}");
        Console.WriteLine($"  • CausationId: {record.Context.CausationId}");
        Console.WriteLine($"  • RequestId: {record.Context.RequestId}\n");

        // ── 4. Platform Event via AuditContext.SystemTenantId ────────────────────
        Console.WriteLine("4. Platform event without tenant specificity (AuditContext.SystemTenantId):");
        var systemRecord = new AuditRecord
        {
            Id = AuditId.NewId(),
            OccurredAt = DateTimeOffset.UtcNow,
            Actor = AuditActor.System,
            Action = AuditAction.Create,
            Resource = new AuditResource("SystemConfiguration", "cfg-global"),
            Outcome = AuditOutcome.Success,
            Context = new AuditContext(
                TenantId: AuditContext.SystemTenantId,     // "system" reserved constant
                Source: "InfrastructureBootstrap")
        };
        await auditStore.AppendAsync(systemRecord);
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"✓ System event with TenantId='{AuditContext.SystemTenantId}' registered.");
        Console.ResetColor();
        Console.WriteLine($"  (AuditContext.SystemTenantId = \"{AuditContext.SystemTenantId}\")\n");

        // ── 5. Basic Query ───────────────────────────────────────────────────────
        Console.WriteLine("5. Query by resource type and tenant:");
        var queryResult = await auditStore.QueryAsync(new AuditQuery
        {
            TenantId = "tenant-acme",
            ResourceType = "CustomerAccount"
        });
        Console.WriteLine($"✓ {queryResult.Records.Count} record(s) found for 'CustomerAccount' in 'tenant-acme'.");
        foreach (var r in queryResult.Records)
        {
            Console.WriteLine($"  [{r.OccurredAt:yyyy-MM-dd HH:mm:ss UTC}] {r.Actor.DisplayName} -> {r.Action.Code} on {r.Resource.Type}/{r.Resource.Id} -> {r.Outcome}");
        }

        // ── 6. Query by ActorId ──────────────────────────────────────────────────
        Console.WriteLine("\n6. Filter by ActorId (AuditQuery.ActorId):");
        var actorQuery = await auditStore.QueryAsync(new AuditQuery
        {
            TenantId = "tenant-acme",
            ActorId = "usr-9872"
        });
        Console.WriteLine($"✓ {actorQuery.Records.Count} record(s) from actor 'usr-9872'.");

        // ── 7. Query by Temporal Range ───────────────────────────────────────────
        Console.WriteLine("\n7. Filter by temporal range (AuditQuery.From / AuditQuery.To):");
        var rangeQuery = await auditStore.QueryAsync(new AuditQuery
        {
            TenantId = "tenant-acme",
            From = DateTimeOffset.UtcNow.AddMinutes(-5),
            To = DateTimeOffset.UtcNow.AddMinutes(1)
        });
        Console.WriteLine($"✓ {rangeQuery.Records.Count} record(s) in the last 5 minutes window.");

        // ── 8. Query by CorrelationId ────────────────────────────────────────────
        Console.WriteLine("\n8. Filter by CorrelationId (AuditQuery.CorrelationId):");
        var correlQuery = await auditStore.QueryAsync(new AuditQuery
        {
            TenantId = "tenant-acme",
            CorrelationId = correlationId
        });
        Console.WriteLine($"✓ {correlQuery.Records.Count} record(s) with CorrelationId='{correlationId[..8]}...'.\n");
    }
}
