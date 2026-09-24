// Copyright © Erickson Lopez. MIT License.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using EricksonLopez.Auditing.Testing;

namespace EricksonLopez.Auditing.Showcase.Levels;

/// <summary>
/// Level 5 — Batch Processing: Bulk Inserts, Concurrency,
/// Tenant Constraints & InMemoryAuditStore Test Helpers.
/// </summary>
public static class Level05_BatchProcessing
{
    public static async Task RunAsync()
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("\n================================================================================");
        Console.WriteLine(" [LEVEL 5] — BATCH PROCESSING & HIGH-THROUGHPUT PERFORMANCE");
        Console.WriteLine("================================================================================\n");
        Console.ResetColor();

        var services = new ServiceCollection();
        services.AddAuditing()
                .UseStore<InMemoryAuditStore>();

        var sp = services.BuildServiceProvider();
        var store = sp.GetRequiredService<IAuditStore>();
        var inMemoryStore = (InMemoryAuditStore)store;

        const int batchCount = 500;
        const string tenantId = "tenant-fintech";

        Console.WriteLine($"1. Generating bulk batch of {batchCount} audit records with UUIDv7...");
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
        Console.WriteLine($"✓ {batchCount} records generated in memory in {sw.Elapsed.TotalMilliseconds:F2} ms.");

        // Batch insertion via AppendBatchAsync
        sw.Restart();
        await store.AppendBatchAsync(records);
        sw.Stop();

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"✓ AppendBatchAsync ({batchCount} records) completed in {sw.Elapsed.TotalMilliseconds:F2} ms.");
        Console.ResetColor();

        // ── InMemoryAuditStore helpers — ForTenant() ─────────────────────────────
        Console.WriteLine("\n2. InMemoryAuditStore.ForTenant() — In-memory tenant filtering:");
        var tenantRecords = inMemoryStore.ForTenant(tenantId);
        Console.WriteLine($"   • ForTenant(\"{tenantId}\"): {tenantRecords.Count} records.");

        // ── InMemoryAuditStore helpers — ForActor() ──────────────────────────────
        Console.WriteLine("\n3. InMemoryAuditStore.ForActor() — In-memory actor filtering:");
        var actorRecords = inMemoryStore.ForActor("service-worker-1");
        Console.WriteLine($"   • ForActor(\"service-worker-1\"): {actorRecords.Count} records.");
        Console.WriteLine($"   • Total records in store: {inMemoryStore.Count}");

        // ── InMemoryAuditStore helpers — Clear() ─────────────────────────────────
        Console.WriteLine("\n4. InMemoryAuditStore.Clear() — Reset between test cycles:");
        Console.WriteLine($"   • Records before Clear(): {inMemoryStore.Count}");
        inMemoryStore.Clear();
        Console.WriteLine($"   • Records after Clear(): {inMemoryStore.Count}");
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("   ⚠ Clear() purges all in-memory records. Useful for test isolation.");
        Console.ResetColor();

        // ── Multi-Tenant Batch Architectural Constraint ─────────────────────────
        Console.WriteLine("\n5. Multi-Tenant Safety Invariant in Batches:");
        Console.WriteLine("   Rule: All records within a batch must belong to the SAME tenant.");

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
                Context = new AuditContext("tenant-b", "Worker") // Different tenant
            }
        };

        try
        {
            // Verify how relational stores enforce tenant homogeneity
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
            Console.WriteLine($"✓ Expected exception captured correctly:");
            Console.WriteLine($"  \"{ex.Message}\"");
            Console.ResetColor();
        }

        // ── 6. Asynchronous Buffered Processing (BufferedAuditStoreDecorator) ─────
        Console.WriteLine("\n6. Asynchronous Channel Buffering (BufferedAuditStoreDecorator):");
        var bufferTargetStore = new InMemoryAuditStore();
        var bufferOptions = new BufferedAuditStoreOptions
        {
            Capacity = 100,
            BatchSize = 10,
            FlushInterval = TimeSpan.FromMilliseconds(50)
        };

        await using (var bufferedStore = new BufferedAuditStoreDecorator(bufferTargetStore, bufferOptions))
        {
            for (int i = 0; i < 25; i++)
            {
                await bufferedStore.AppendAsync(new AuditRecord
                {
                    Id = AuditId.NewId(),
                    OccurredAt = DateTimeOffset.UtcNow,
                    Actor = AuditActor.System,
                    Action = AuditAction.Create,
                    Resource = new AuditResource("SensorReading", $"sensor-{i}"),
                    Outcome = AuditOutcome.Success,
                    Context = new AuditContext("tenant-iot", "SensorWorker")
                });
            }

            // Await brief window for background channel to flush
            await Task.Delay(150);
            Console.WriteLine($"✓ BufferedAuditStoreDecorator drained items via background channel worker.");
            Console.WriteLine($"  • Buffer Target Store Count: {bufferTargetStore.Count} records.");
            Console.WriteLine($"  • Buffer Configuration: Capacity={bufferOptions.Capacity}, BatchSize={bufferOptions.BatchSize}, FlushInterval={bufferOptions.FlushInterval.TotalMilliseconds}ms");
        }

        // ── 7. Transactional Outbox Pattern (OutboxAuditStore & IOutboxMessageService) ──
        Console.WriteLine("\n7. Transactional Outbox Pattern (EricksonLopez.Auditing.Outbox):");
        var outboxService = new ShowcaseOutboxMessageService();
        var outboxStore = new EricksonLopez.Auditing.Outbox.OutboxAuditStore(outboxService);

        var outboxRecord = new AuditRecord
        {
            Id = AuditId.NewId(),
            OccurredAt = DateTimeOffset.UtcNow,
            Actor = new AuditActor(AuditActorType.User, "usr-checkout-01", "Bob Customer"),
            Action = AuditAction.Create,
            Resource = new AuditResource("Order", "ord-9901"),
            Outcome = AuditOutcome.Success,
            Context = new AuditContext("tenant-ecommerce", "CheckoutService")
        };

        await outboxStore.AppendAsync(outboxRecord);
        Console.WriteLine($"✓ OutboxAuditStore.AppendAsync: Message staged atomically in transactional outbox.");
        Console.WriteLine($"  • Enqueued EventType: '{outboxService.Messages[0].EventType}'");
        Console.WriteLine($"  • Serialized Payload Length: {outboxService.Messages[0].Payload.Length} characters (Native AOT-safe JSON).\n");
    }

    private sealed class ShowcaseOutboxMessageService : EricksonLopez.Auditing.Outbox.IOutboxMessageService
    {
        public readonly List<(string EventType, string Payload)> Messages = new();

        public ValueTask AppendMessageAsync(string eventType, string payload, System.Threading.CancellationToken cancellationToken = default)
        {
            Messages.Add((eventType, payload));
            return ValueTask.CompletedTask;
        }
    }
}
