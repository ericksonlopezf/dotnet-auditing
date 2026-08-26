// Copyright © Erickson Lopez. MIT License.
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using EricksonLopez.Auditing.Testing;

namespace EricksonLopez.Auditing.Showcase.Levels;

/// <summary>
/// Nivel 4 — Integración Avanzada: Scopes Ambientales, Anidamiento y Enriquecimiento de Contexto
/// </summary>
public static class Level04_AdvancedIntegration
{
    public static async Task RunAsync()
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("\n================================================================================");
        Console.WriteLine(" [NIVEL 4] — INTEGRACIÓN AVANZADA: AUDITSCOPE Y CONTEXTO AMBIENTAL");
        Console.WriteLine("================================================================================\n");
        Console.ResetColor();

        var services = new ServiceCollection();
        services.AddAuditing()
                .UseStore<InMemoryAuditStore>();

        var sp = services.BuildServiceProvider();
        var store = sp.GetRequiredService<IAuditStore>();

        Console.WriteLine("1. Creación de Scope Padre (Transacción Principal)");
        using (var parentScope = AuditScope.Begin(new Dictionary<string, string>
        {
            ["WorkflowId"] = "wf-order-fulfillment-880",
            ["TriggeredBy"] = "ScheduleWorker"
        }))
        {
            parentScope.WithMetadata("Environment", "Production");

            Console.WriteLine($"   • Scope Padre Activo: WorkflowId={AuditScope.Current?.Metadata["WorkflowId"]}, Environment={AuditScope.Current?.Metadata["Environment"]}");

            // Crear registro en el scope padre
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
            Console.WriteLine("   ✓ Registro creado bajo Scope Padre.");

            Console.WriteLine("\n2. Creación de Scope Hijo Anidado (Sub-operación de Pago)");
            using (var childScope = AuditScope.Begin(new Dictionary<string, string>
            {
                ["SubStep"] = "CapturePayment",
                ["PaymentProcessor"] = "Stripe"
            }))
            {
                childScope.WithMetadata("Attempt", "1");
                Console.WriteLine($"   • Scope Hijo Activo: SubStep={AuditScope.Current?.Metadata["SubStep"]}, Attempt={AuditScope.Current?.Metadata["Attempt"]}");

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
                Console.WriteLine("   ✓ Registro creado bajo Scope Hijo.");
            }

            Console.WriteLine("\n3. Verificación de Restauración del Scope Padre tras Dispose del Scope Hijo");
            Console.WriteLine($"   • Scope Actual Restaurado: WorkflowId={AuditScope.Current?.Metadata["WorkflowId"]} (¿Existe SubStep?: {AuditScope.Current?.Metadata.ContainsKey("SubStep")})");
        }

        Console.WriteLine("\n4. Verificación tras Dispose del Scope Padre:");
        Console.WriteLine($"   • AuditScope.Current es null: {AuditScope.Current is null}\n");
    }
}
