// Copyright © Erickson Lopez. MIT License.
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using EricksonLopez.Auditing.Testing;

namespace EricksonLopez.Auditing.Showcase.Levels;

/// <summary>
/// Level 4 — Advanced Integration: Ambient Scopes, Nesting & Context Enrichment
/// </summary>
public static class Level04_AdvancedIntegration
{
    public static async Task RunAsync()
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("\n================================================================================");
        Console.WriteLine(" [LEVEL 4] — ADVANCED INTEGRATION: AUDITSCOPE & AMBIENT CONTEXT");
        Console.WriteLine("================================================================================\n");
        Console.ResetColor();

        var services = new ServiceCollection();
        services.AddAuditing()
                .UseStore<InMemoryAuditStore>();

        var sp = services.BuildServiceProvider();
        var store = sp.GetRequiredService<IAuditStore>();

        Console.WriteLine("1. Creating Parent Scope (Main Transaction)");
        using (var parentScope = AuditScope.Begin(new Dictionary<string, string>
        {
            ["WorkflowId"] = "wf-order-fulfillment-880",
            ["TriggeredBy"] = "ScheduleWorker"
        }))
        {
            parentScope.WithMetadata("Environment", "Production");

            Console.WriteLine($"   • Active Parent Scope: WorkflowId={AuditScope.Current?.Metadata["WorkflowId"]}, Environment={AuditScope.Current?.Metadata["Environment"]}");

            // Create record within parent scope
            var parentRecord = new AuditRecord
            {
                Id = AuditId.NewId(),
                OccurredAt = DateTimeOffset.UtcNow,
                Actor = AuditActor.System,
                Action = AuditAction.Create,
                Resource = new AuditResource("WorkflowInstance", "wf-order-fulfillment-880"),
                Outcome = AuditOutcome.Success,
                Context = new AuditContext(
                    TenantId: "tenant-enterprise",
                    Source: "WorkflowEngine",
                    CorrelationId: AuditScope.Current?.Metadata["WorkflowId"])
            };
            await store.AppendAsync(parentRecord);
            Console.WriteLine("   ✓ Record created under Parent Scope.");

            Console.WriteLine("\n2. Creating Nested Child Scope (Payment Sub-operation)");
            using (var childScope = AuditScope.Begin(new Dictionary<string, string>
            {
                ["SubStep"] = "CapturePayment",
                ["PaymentProcessor"] = "Stripe"
            }))
            {
                childScope.WithMetadata("Attempt", "1");
                Console.WriteLine($"   • Active Child Scope: SubStep={AuditScope.Current?.Metadata["SubStep"]}, Attempt={AuditScope.Current?.Metadata["Attempt"]}");

                var childRecord = new AuditRecord
                {
                    Id = AuditId.NewId(),
                    OccurredAt = DateTimeOffset.UtcNow,
                    Actor = AuditActor.System,
                    Action = CustomDomainActions.ProcessPayment,
                    Resource = new AuditResource("PaymentTransaction", "tx-stripe-9941"),
                    Outcome = AuditOutcome.Success,
                    Context = new AuditContext(
                        TenantId: "tenant-enterprise",
                        Source: "PaymentWorker",
                        CorrelationId: parentScope.Metadata["WorkflowId"],
                        CausationId: parentRecord.Id.ToString())
                };
                await store.AppendAsync(childRecord);
                Console.WriteLine("   ✓ Record created under Child Scope.");
            }

            Console.WriteLine("\n3. Verifying Parent Scope Restoration after Child Scope Dispose");
            Console.WriteLine($"   • Restored Scope: WorkflowId={AuditScope.Current?.Metadata["WorkflowId"]} (SubStep exists: {AuditScope.Current?.Metadata.ContainsKey("SubStep")})");
        }

        Console.WriteLine("\n4. Verification after Parent Scope Dispose:");
        Console.WriteLine($"   • AuditScope.Current is null: {AuditScope.Current is null}\n");
    }
}
