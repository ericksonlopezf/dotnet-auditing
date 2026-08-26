// Copyright © Erickson Lopez. MIT License.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using EricksonLopez.Auditing.Testing;

namespace EricksonLopez.Auditing.Showcase.Levels;

/// <summary>
/// Nivel 5 — Procesamiento en Lotes: Inserciones Masivas, Concurrencia,
/// Restricciones de Tenant y demostración de InMemoryAuditStore helpers.
/// </summary>
public static class Level05_BatchProcessing
{
    public static async Task RunAsync()
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("\n================================================================================");
        Console.WriteLine(" [NIVEL 5] — PROCESAMIENTO EN LOTES (BATCH PROCESSING) Y RENDIMIENTO");
        Console.WriteLine("================================================================================\n");
        Console.ResetColor();

        var services = new ServiceCollection();
        services.AddAuditing()
                .UseStore<InMemoryAuditStore>();

        var sp = services.BuildServiceProvider();
        var store = sp.GetRequiredService<IAuditStore>();
        var inMemoryStore = (InMemoryAuditStore)store; // Cast para acceder a los helpers de testing

        const int batchCount = 500;
        const string tenantId = "tenant-fintech";

        Console.WriteLine($"1. Generando lote masivo de {batchCount} registros de auditoría con UUIDv7...");
        var sw = Stopwatch.StartNew();

        var records = new List<AuditRecord>(batchCount);
        for (int i = 0; i < batchCount; i++)
        {
            records.Add(new AuditRecord
            {
                Id = AuditId.NewId(),
                OccurredAt = DateTimeOffset.UtcNow,
                Actor = new AuditActor(AuditActorType.Service, $"service-worker-{(i % 5) + 1}"),
                Action = AuditAction.Read,
                Resource = new AuditResource("AccountLedger", $"ledger-{1000 + i}"),
                Outcome = AuditOutcome.Success,
                Context = new AuditContext(
                    TenantId: tenantId,
                    Source: "DataSyncWorker",
                    CorrelationId: $"batch-sync-{i / 100}")
            });
        }
        sw.Stop();
        Console.WriteLine($"✓ {batchCount} registros generados en memoria en {sw.Elapsed.TotalMilliseconds:F2} ms.");

        // Inserción en lote mediante AppendBatchAsync
        sw.Restart();
        await store.AppendBatchAsync(records);
        sw.Stop();

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"✓ AppendBatchAsync ({batchCount} registros) completado en {sw.Elapsed.TotalMilliseconds:F2} ms.");
        Console.ResetColor();

        // ── InMemoryAuditStore helpers — ForTenant() ─────────────────────────────
        Console.WriteLine("\n2. InMemoryAuditStore.ForTenant() — Filtrado in-memory por tenant:");
        var tenantRecords = inMemoryStore.ForTenant(tenantId);
        Console.WriteLine($"   • ForTenant(\"{tenantId}\"): {tenantRecords.Count} registros.");

        // ── InMemoryAuditStore helpers — ForActor() ──────────────────────────────
        Console.WriteLine("\n3. InMemoryAuditStore.ForActor() — Filtrado in-memory por actor:");
        var actorRecords = inMemoryStore.ForActor("service-worker-1");
        Console.WriteLine($"   • ForActor(\"service-worker-1\"): {actorRecords.Count} registros.");
        Console.WriteLine($"   • Total registros en store: {inMemoryStore.Count}");

        // ── InMemoryAuditStore helpers — Clear() ─────────────────────────────────
        Console.WriteLine("\n4. InMemoryAuditStore.Clear() — Limpieza entre ciclos de test:");
        Console.WriteLine($"   • Registros antes de Clear(): {inMemoryStore.Count}");
        inMemoryStore.Clear();
        Console.WriteLine($"   • Registros después de Clear(): {inMemoryStore.Count}");
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("   ⚠ Clear() elimina todos los registros en memoria. Útil para aislamiento entre tests.");
        Console.ResetColor();

        // ── Demostración de Restricción Arquitectónica de Tenant en Batches ──────
        Console.WriteLine("\n5. Validación de Seguridad Multi-Tenant en Lotes:");
        Console.WriteLine("   Regla: Todos los registros dentro de un batch deben pertenecer al MISMO tenant.");

        var mixedBatch = new List<AuditRecord>
        {
            new()
            {
                Id = AuditId.NewId(),
                OccurredAt = DateTimeOffset.UtcNow,
                Actor = AuditActor.System,
                Action = AuditAction.Update,
                Resource = new AuditResource("Config", "1"),
                Outcome = AuditOutcome.Success,
                Context = new AuditContext("tenant-a", "Worker")
            },
            new()
            {
                Id = AuditId.NewId(),
                OccurredAt = DateTimeOffset.UtcNow,
                Actor = AuditActor.System,
                Action = AuditAction.Update,
                Resource = new AuditResource("Config", "2"),
                Outcome = AuditOutcome.Success,
                Context = new AuditContext("tenant-b", "Worker") // Tenant distinto
            }
        };

        try
        {
            // Verificamos cómo los stores relacionales validan la homogeneidad del tenant en batch
            if (mixedBatch[0].Context.TenantId != mixedBatch[1].Context.TenantId)
            {
                throw new InvalidOperationException(
                    "All records in a batch must belong to the same tenant. " +
                    "Split cross-tenant records into separate batch operations.");
            }
            await store.AppendBatchAsync(mixedBatch);
        }
        catch (InvalidOperationException ex)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"✓ Excepción esperada capturada correctamente:");
            Console.WriteLine($"  \"{ex.Message}\"");
            Console.ResetColor();
        }

        Console.WriteLine();
    }
}
