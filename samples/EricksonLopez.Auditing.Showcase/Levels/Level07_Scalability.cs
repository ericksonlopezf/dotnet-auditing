// Copyright © Erickson Lopez. MIT License.
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using EricksonLopez.Auditing.Testing;

namespace EricksonLopez.Auditing.Showcase.Levels;

/// <summary>
/// Nivel 7 — Escalabilidad: Paginación por Cursor (Keyset Pagination) y Filtros Avanzados de AuditQuery
/// </summary>
public static class Level07_Scalability
{
    public static async Task RunAsync()
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("\n================================================================================");
        Console.WriteLine(" [NIVEL 7] — ESCALABILIDAD: PAGINACIÓN POR CURSOR Y FILTROS AVANZADOS");
        Console.WriteLine("================================================================================\n");
        Console.ResetColor();

        var services = new ServiceCollection();
        services.AddAuditing()
                .UseStore<InMemoryAuditStore>();

        var sp = services.BuildServiceProvider();
        var store = sp.GetRequiredService<IAuditStore>();

        const string tenantId = "tenant-high-scale";
        const int totalRecords = 25;
        const int pageSize = 7;
        const string correlationGroup = "corr-batch-import-2026";

        Console.WriteLine($"1. Sembrando {totalRecords} registros cronológicos para el tenant '{tenantId}'...");
        var seedRecords = new List<AuditRecord>(totalRecords);
        var baseTime = DateTimeOffset.UtcNow.AddMinutes(-totalRecords);

        // 3 actores distintos para demostrar filtro por actor
        string[] actors = { "user-alice-01", "user-bob-02", "user-carol-03" };

        for (int i = 1; i <= totalRecords; i++)
        {
            seedRecords.Add(new AuditRecord
            {
                Id = AuditId.NewId(),
                OccurredAt = baseTime.AddSeconds(i * 2),
                Actor = new AuditActor(AuditActorType.User, actors[(i - 1) % 3]),
                Action = AuditAction.Read,
                Resource = new AuditResource("Document", $"doc-{i:D3}"),
                Outcome = AuditOutcome.Success,
                Context = new AuditContext(
                    TenantId: tenantId,
                    Source: "DocumentService",
                    CorrelationId: i <= 15 ? correlationGroup : null) // Solo 15 con correlationId
            });
        }
        await store.AppendBatchAsync(seedRecords);
        Console.WriteLine($"✓ {totalRecords} registros sembrados exitosamente.\n");

        // ── 2. Keyset Cursor Pagination ──────────────────────────────────────────
        Console.WriteLine($"2. Recorriendo páginas con Keyset Cursor (PageSize = {pageSize}):");
        Console.WriteLine("   ┌──────┬──────────────────┬──────────────────────────────────────┬─────────┐");
        Console.WriteLine("   │ Pág. │ Registros leídos │ Último Record ID (Cursor)            │ HasMore │");
        Console.WriteLine("   ├──────┼──────────────────┼──────────────────────────────────────┼─────────┤");

        Guid? cursor = null;
        int pageNumber = 1;

        while (true)
        {
            var query = new AuditQuery
            {
                TenantId = tenantId,
                PageSize = pageSize,
                AfterRecordId = cursor
            };

            var result = await store.QueryAsync(query);

            Console.WriteLine($"   │ {pageNumber,4} │ {result.Records.Count,16} │ {result.NextCursorId?.ToString() ?? "null",-36} │ {result.HasMore,-7} │");

            if (!result.HasMore || result.NextCursorId is null)
            {
                break;
            }

            cursor = result.NextCursorId;
            pageNumber++;
        }
        Console.WriteLine("   └──────┴──────────────────┴──────────────────────────────────────┴─────────┘\n");

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("✓ Ventajas de Keyset Pagination frente a OFFSET/LIMIT:");
        Console.WriteLine("  • Complejidad O(1) index seek en cada página sin escanear filas previas.");
        Console.WriteLine("  • Inmune a corrimiento o duplicación cuando se insertan nuevos registros concurrentemente.");
        Console.WriteLine("  • Diseñado para tablas particionadas por tiempo en PostgreSQL, SQL Server, MySQL y Oracle.\n");
        Console.ResetColor();

        // ── 3. Filtro por ActorId ────────────────────────────────────────────────
        Console.WriteLine("3. Filtro por ActorId (AuditQuery.ActorId):");
        var byActorQuery = await store.QueryAsync(new AuditQuery
        {
            TenantId = tenantId,
            ActorId = "user-alice-01",
            PageSize = 100
        });
        Console.WriteLine($"   • Registros de 'user-alice-01': {byActorQuery.Records.Count}");

        // ── 4. Filtro por CorrelationId ──────────────────────────────────────────
        Console.WriteLine("\n4. Filtro por CorrelationId (AuditQuery.CorrelationId):");
        var byCorrelation = await store.QueryAsync(new AuditQuery
        {
            TenantId = tenantId,
            CorrelationId = correlationGroup,
            PageSize = 100
        });
        Console.WriteLine($"   • Registros con CorrelationId='{correlationGroup}': {byCorrelation.Records.Count}");
        Console.WriteLine("   (Traza todos los eventos de la misma operación distribuida).");

        // ── 5. Filtro por rango de fechas (From / To) ────────────────────────────
        Console.WriteLine("\n5. Filtro por Rango Temporal (AuditQuery.From / AuditQuery.To):");
        var from = baseTime.AddSeconds(1);
        var to = baseTime.AddSeconds(15 * 2);
        var byDateRange = await store.QueryAsync(new AuditQuery
        {
            TenantId = tenantId,
            From = from,
            To = to,
            PageSize = 100
        });
        Console.WriteLine($"   • Registros entre {from:HH:mm:ss} y {to:HH:mm:ss} UTC: {byDateRange.Records.Count}");

        // ── 6. Filtro por Outcome ────────────────────────────────────────────────
        Console.WriteLine("\n6. Filtro por Outcome (AuditQuery.Outcome):");
        var byOutcome = await store.QueryAsync(new AuditQuery
        {
            TenantId = tenantId,
            Outcome = AuditOutcome.Success,
            PageSize = 100
        });
        Console.WriteLine($"   • Registros con Outcome=Success: {byOutcome.Records.Count}");

        // ── 7. Filtro combinado (ActionCode + ResourceType) ─────────────────────
        Console.WriteLine("\n7. Filtro Combinado (ActionCode + ResourceType — AND semántico):");
        var combined = await store.QueryAsync(new AuditQuery
        {
            TenantId = tenantId,
            ActionCode = AuditAction.Read.Code,
            ResourceType = "Document",
            PageSize = 100
        });
        Console.WriteLine($"   • Registros con Action=Read AND ResourceType=Document: {combined.Records.Count}\n");
    }
}
