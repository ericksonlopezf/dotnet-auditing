// Copyright © Erickson Lopez. MIT License.
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using EricksonLopez.Auditing.Testing;

namespace EricksonLopez.Auditing.Showcase.Levels;

/// <summary>
/// Nivel 6 — Manejo de Errores y Semántica de Fallos (Fail Semantics)
/// </summary>
public static class Level06_ErrorHandling
{
    public static async Task RunAsync()
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("\n================================================================================");
        Console.WriteLine(" [NIVEL 6] — MANEJO DE ERRORES Y POLÍTICAS DE FALLO (FAIL SEMANTICS)");
        Console.WriteLine("================================================================================\n");
        Console.ResetColor();

        Console.WriteLine("1. Matriz de Comportamiento ante Fallas de Almacenamiento:");
        Console.WriteLine("   ┌────────────────┬─────────────────────────────────────────────────────────────┐");
        Console.WriteLine("   │ Política       │ Semántica y Casos de Uso Recomendados                       │");
        Console.WriteLine("   ├────────────────┼─────────────────────────────────────────────────────────────┤");
        Console.WriteLine("   │ FailClosed (1) │ Propaga la excepción y aborta la operación de negocio.      │");
        Console.WriteLine("   │                │ Uso: Operaciones financieras, compliance estricto, auth.    │");
        Console.WriteLine("   ├────────────────┼─────────────────────────────────────────────────────────────┤");
        Console.WriteLine("   │ FailOpen (2)   │ Captura la excepción y permite que el negocio continúe.     │");
        Console.WriteLine("   │                │ Uso: Consultas de lectura frecuentes, tracking no crítico.  │");
        Console.WriteLine("   ├────────────────┼─────────────────────────────────────────────────────────────┤");
        Console.WriteLine("   │ Deferred (3)   │ Encola el registro en un mecanismo durable (Transactional   │");
        Console.WriteLine("   │                │ Outbox) para procesamiento asíncrono garantizado.           │");
        Console.WriteLine("   └────────────────┴─────────────────────────────────────────────────────────────┘\n");

        var services = new ServiceCollection();
        services.AddAuditing(cfg =>
        {
            cfg.DefaultFailureBehavior = AuditFailureBehavior.FailClosed;
            cfg.CriticalActionCodes.Add("ProcessPayroll");
            cfg.CriticalActionCodes.Add("RevokeCertificate");
        })
        .UseStore<InMemoryAuditStore>();

        var sp = services.BuildServiceProvider();
        var store = sp.GetRequiredService<IAuditStore>();

        // 2. Registro de Outcomes con Códigos de Error Estandarizados
        Console.WriteLine("2. Clasificación de Resultados de Auditoría con Códigos Seguros:");

        var failureOutcomes = new (AuditOutcome Outcome, string ActionCode, string ErrorCode, string Description)[]
        {
            (AuditOutcome.Denied, AuditAction.Delete.Code, "AUTHZ_INSUFFICIENT_ROLE", "Acción de eliminación rechazada por falta de rol"),
            (AuditOutcome.Failure, "ProcessPayroll", "PAYMENT_GATEWAY_TIMEOUT", "Fallo técnico en la pasarela de pago"),
            (AuditOutcome.Cancelled, "BatchReportGeneration", "USER_REQUEST_CANCELLED", "Operación abortada explícitamente por el usuario"),
            (AuditOutcome.Partial, "BulkUserImport", "PARTIAL_CSV_VALIDATION_ERR", "Importación completada con filas inválidas")
        };

        foreach (var item in failureOutcomes)
        {
            var record = new AuditRecord
            {
                Id = AuditId.NewId(),
                OccurredAt = DateTimeOffset.UtcNow,
                Actor = new AuditActor(AuditActorType.User, "usr-operator-01"),
                Action = new AuditAction(item.ActionCode),
                Resource = new AuditResource("SecurityTarget", "res-901"),
                Outcome = item.Outcome,
                ErrorCode = item.ErrorCode,
                Context = new AuditContext("tenant-compliance", "AuditShowcase")
            };

            await store.AppendAsync(record);
            Console.WriteLine($"   • Outcome: {item.Outcome,-10} | Action: {item.ActionCode,-22} | ErrorCode: {item.ErrorCode,-28} | {item.Description}");
        }

        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("\n✓ Invariante de Seguridad en ErrorCode: Nunca debe almacenar mensajes de excepción");
        Console.WriteLine("  crudos, cadenas de conexión o stack traces, evitando fugas de información forense.\n");
        Console.ResetColor();
    }
}
