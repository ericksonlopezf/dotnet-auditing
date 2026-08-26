// Copyright © Erickson Lopez. MIT License.
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using EricksonLopez.Auditing.Testing;

namespace EricksonLopez.Auditing.Showcase.Levels;

/// <summary>
/// Nivel 1 — Inicio Rápido: Configuración Mínima, DI y Primer Registro de Auditoría
/// </summary>
public static class Level01_QuickStart
{
    public static async Task RunAsync()
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("\n================================================================================");
        Console.WriteLine(" [NIVEL 1] — INICIO RÁPIDO: CONFIGURACIÓN MÍNIMA Y PRIMER REGISTRO");
        Console.WriteLine("================================================================================\n");
        Console.ResetColor();

        // ── 1. Configuración de Inyección de Dependencias ────────────────────────
        var services = new ServiceCollection();
        services.AddAuditing()
                .UseStore<InMemoryAuditStore>();

        var serviceProvider = services.BuildServiceProvider();
        var auditStore = serviceProvider.GetRequiredService<IAuditStore>();

        Console.WriteLine("✓ Servicios de auditoría registrados en IServiceCollection.");
        Console.WriteLine("✓ Store configurado: InMemoryAuditStore (para pruebas / desarrollo rápido).\n");

        // ── 2. Construcción de un AuditRecord canónico ───────────────────────────
        var correlationId = Guid.NewGuid().ToString("N");
        var record = new AuditRecord
        {
            Id = AuditId.NewId(),                          // Generación UUIDv7 con orden cronológico
            OccurredAt = DateTimeOffset.UtcNow,
            Actor = new AuditActor(AuditActorType.User, "usr-9872", "alice@example.com"),
            Action = AuditAction.Create,
            Resource = new AuditResource("CustomerAccount", "acc-5541"),
            Outcome = AuditOutcome.Success,
            Context = new AuditContext(
                TenantId: "tenant-acme",
                Source: "CustomerOnboardingService",
                CorrelationId: correlationId,
                CausationId: "cmd-create-account-99",     // Comando que originó esta acción
                RequestId: "req-http-8812",               // HTTP Request ID del transporte
                IpAddress: "10.0.0.55",
                UserAgent: "Mozilla/5.0 (Windows NT 10.0)"),
            Changes = new[]
            {
                new AuditChange("AccountName", null, "Acme Corp"),
                new AuditChange("Tier", null, "Enterprise")
            }
        };

        // ── 3. Persistir en el almacén ───────────────────────────────────────────
        await auditStore.AppendAsync(record);
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"✓ Registro persistido con ID (UUIDv7): {record.Id}");
        Console.ResetColor();
        Console.WriteLine($"  • Actor: {record.Actor.DisplayName} ({record.Actor.Id}) [{record.Actor.Type}]");
        Console.WriteLine($"  • Acción: {record.Action.Code}");
        Console.WriteLine($"  • Recurso: {record.Resource.Type}:{record.Resource.Id}");
        Console.WriteLine($"  • Resultado: {record.Outcome}");
        Console.WriteLine($"  • Tenant: {record.Context.TenantId}");
        Console.WriteLine($"  • CorrelationId: {record.Context.CorrelationId}");
        Console.WriteLine($"  • CausationId: {record.Context.CausationId}");
        Console.WriteLine($"  • RequestId: {record.Context.RequestId}\n");

        // ── 4. Evento de plataforma usando AuditContext.SystemTenantId ───────────
        Console.WriteLine("4. Evento de plataforma sin tenant específico (AuditContext.SystemTenantId):");
        var systemRecord = new AuditRecord
        {
            Id = AuditId.NewId(),
            OccurredAt = DateTimeOffset.UtcNow,
            Actor = AuditActor.System,                     // Actor predefinido para sistema
            Action = AuditAction.Create,
            Resource = new AuditResource("SystemConfiguration", "cfg-global"),
            Outcome = AuditOutcome.Success,
            Context = new AuditContext(
                TenantId: AuditContext.SystemTenantId,     // "system" — constante reservada
                Source: "InfrastructureBootstrap")
        };
        await auditStore.AppendAsync(systemRecord);
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"✓ Evento de sistema con TenantId='{AuditContext.SystemTenantId}' registrado.");
        Console.ResetColor();
        Console.WriteLine($"  (AuditContext.SystemTenantId = \"{AuditContext.SystemTenantId}\")\n");

        // ── 5. Consulta básica ───────────────────────────────────────────────────
        Console.WriteLine("5. Consulta por tipo de recurso y tenant:");
        var queryResult = await auditStore.QueryAsync(new AuditQuery
        {
            TenantId = "tenant-acme",
            ResourceType = "CustomerAccount"
        });
        Console.WriteLine($"✓ {queryResult.Records.Count} registro(s) encontrado(s) para 'CustomerAccount' en 'tenant-acme'.");
        foreach (var r in queryResult.Records)
        {
            Console.WriteLine($"  [{r.OccurredAt:yyyy-MM-dd HH:mm:ss UTC}] {r.Actor.DisplayName} -> {r.Action.Code} on {r.Resource.Type}/{r.Resource.Id} -> {r.Outcome}");
        }

        // ── 6. Filtro por ActorId ────────────────────────────────────────────────
        Console.WriteLine("\n6. Filtro por ActorId (AuditQuery.ActorId):");
        var actorQuery = await auditStore.QueryAsync(new AuditQuery
        {
            TenantId = "tenant-acme",
            ActorId = "usr-9872"
        });
        Console.WriteLine($"✓ {actorQuery.Records.Count} registro(s) del actor 'usr-9872'.");

        // ── 7. Filtro por rango de fechas (From / To) ────────────────────────────
        Console.WriteLine("\n7. Filtro por rango temporal (AuditQuery.From / AuditQuery.To):");
        var rangeQuery = await auditStore.QueryAsync(new AuditQuery
        {
            TenantId = "tenant-acme",
            From = DateTimeOffset.UtcNow.AddMinutes(-5),   // Ventana de 5 minutos hacia atrás
            To = DateTimeOffset.UtcNow.AddMinutes(1)
        });
        Console.WriteLine($"✓ {rangeQuery.Records.Count} registro(s) en el rango de los últimos 5 minutos.");

        // ── 8. Filtro por CorrelationId ──────────────────────────────────────────
        Console.WriteLine("\n8. Filtro por CorrelationId (AuditQuery.CorrelationId):");
        var correlQuery = await auditStore.QueryAsync(new AuditQuery
        {
            TenantId = "tenant-acme",
            CorrelationId = correlationId
        });
        Console.WriteLine($"✓ {correlQuery.Records.Count} registro(s) con CorrelationId='{correlationId[..8]}...'.\n");
    }
}
