// Copyright © Erickson Lopez. MIT License.
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using EricksonLopez.Auditing.Testing;

namespace EricksonLopez.Auditing.Showcase.Levels;

/// <summary>
/// Level 7 — Scalability: Keyset Cursor Pagination & Advanced AuditQuery Filters
/// </summary>
public static class Level07_Scalability
{
    public static async Task RunAsync()
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("\n================================================================================");
        Console.WriteLine(" [LEVEL 7] — SCALABILITY: KEYSET CURSOR PAGINATION & ADVANCED FILTERS");
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

        Console.WriteLine($"1. Seeding {totalRecords} chronological records for tenant '{tenantId}'...");
        var seedRecords = new List<AuditRecord>(totalRecords);
        var baseTime = DateTimeOffset.UtcNow.AddMinutes(-totalRecords);

        // 3 different actors to demonstrate actor filtering
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
                    CorrelationId: i <= 15 ? correlationGroup : null)
            });
        }
        await store.AppendBatchAsync(seedRecords);
        Console.WriteLine($"✓ {totalRecords} records seeded successfully.\n");

        // ── 2. Keyset Cursor Pagination ──────────────────────────────────────────
        Console.WriteLine($"2. Iterating pages via Keyset Cursor (PageSize = {pageSize}):");
        Console.WriteLine("   ┌──────┬──────────────────┬──────────────────────────────────────┬─────────┐");
        Console.WriteLine("   │ Page │ Records Read     │ Last Record ID (Cursor)              │ HasMore │");
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
        Console.WriteLine("✓ Advantages of Keyset Pagination over OFFSET/LIMIT:");
        Console.WriteLine("  • Constant O(1) index seek complexity per page without scanning preceding rows.");
        Console.WriteLine("  • Immune to row shifting or duplication when new records are concurrently inserted.");
        Console.WriteLine("  • Optimized for time-partitioned tables in PostgreSQL, SQL Server, MySQL, and Oracle.\n");
        Console.ResetColor();

        // ── 3. Query by ActorId ──────────────────────────────────────────────────
        Console.WriteLine("3. Filter by ActorId (AuditQuery.ActorId):");
        var byActorQuery = await store.QueryAsync(new AuditQuery
        {
            TenantId = tenantId,
            ActorId = "user-alice-01",
            PageSize = 100
        });
        Console.WriteLine($"   • Records from 'user-alice-01': {byActorQuery.Records.Count}");

        // ── 4. Query by CorrelationId ────────────────────────────────────────────
        Console.WriteLine("\n4. Filter by CorrelationId (AuditQuery.CorrelationId):");
        var byCorrelation = await store.QueryAsync(new AuditQuery
        {
            TenantId = tenantId,
            CorrelationId = correlationGroup,
            PageSize = 100
        });
        Console.WriteLine($"   • Records with CorrelationId='{correlationGroup}': {byCorrelation.Records.Count}");
        Console.WriteLine("   (Traces all events belonging to the same distributed transaction).");

        // ── 5. Query by Date Range ───────────────────────────────────────────────
        Console.WriteLine("\n5. Filter by Temporal Range (AuditQuery.From / AuditQuery.To):");
        var from = baseTime.AddSeconds(1);
        var to = baseTime.AddSeconds(15 * 2);
        var byDateRange = await store.QueryAsync(new AuditQuery
        {
            TenantId = tenantId,
            From = from,
            To = to,
            PageSize = 100
        });
        Console.WriteLine($"   • Records between {from:HH:mm:ss} and {to:HH:mm:ss} UTC: {byDateRange.Records.Count}");

        // ── 6. Query by Outcome ──────────────────────────────────────────────────
        Console.WriteLine("\n6. Filter by Outcome (AuditQuery.Outcome):");
        var byOutcome = await store.QueryAsync(new AuditQuery
        {
            TenantId = tenantId,
            Outcome = AuditOutcome.Success,
            PageSize = 100
        });
        Console.WriteLine($"   • Records with Outcome=Success: {byOutcome.Records.Count}");

        // ── 7. Combined Filter ───────────────────────────────────────────────────
        Console.WriteLine("\n7. Combined Filter (ActionCode + ResourceType — Semantic AND):");
        var combined = await store.QueryAsync(new AuditQuery
        {
            TenantId = tenantId,
            ActionCode = AuditAction.Read.Code,
            ResourceType = "Document",
            PageSize = 100
        });
        Console.WriteLine($"   • Records with Action=Read AND ResourceType=Document: {combined.Records.Count}\n");
    }
}
