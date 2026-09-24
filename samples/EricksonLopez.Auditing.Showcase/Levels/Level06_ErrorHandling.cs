// Copyright © Erickson Lopez. MIT License.
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using EricksonLopez.Auditing.Testing;

namespace EricksonLopez.Auditing.Showcase.Levels;

/// <summary>
/// Level 6 — Error Handling & Failure Policies (Fail Semantics)
/// </summary>
public static class Level06_ErrorHandling
{
    public static async Task RunAsync()
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("\n================================================================================");
        Console.WriteLine(" [LEVEL 6] — ERROR HANDLING & FAILURE POLICIES (FAIL SEMANTICS)");
        Console.WriteLine("================================================================================\n");
        Console.ResetColor();

        Console.WriteLine("1. Storage Failure Policy Matrix:");
        Console.WriteLine("   ┌────────────────┬─────────────────────────────────────────────────────────────┐");
        Console.WriteLine("   │ Policy         │ Semantics & Recommended Use Cases                           │");
        Console.WriteLine("   ├────────────────┼─────────────────────────────────────────────────────────────┤");
        Console.WriteLine("   │ FailClosed (1) │ Propagates exception and aborts business operation.         │");
        Console.WriteLine("   │                │ Usage: Financial operations, strict compliance, auth.       │");
        Console.WriteLine("   ├────────────────┼─────────────────────────────────────────────────────────────┤");
        Console.WriteLine("   │ FailOpen (2)   │ Catches exception and allows business flow to proceed.      │");
        Console.WriteLine("   │                │ Usage: Frequent read queries, non-critical tracking.        │");
        Console.WriteLine("   ├────────────────┼─────────────────────────────────────────────────────────────┤");
        Console.WriteLine("   │ Deferred (3)   │ Enqueues record into durable storage (Transactional Outbox) │");
        Console.WriteLine("   │                │ for guaranteed asynchronous processing.                     │");
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

        // 2. Record Outcomes with Standardized Error Codes
        Console.WriteLine("2. Audit Outcome Classification with Secure Error Codes:");

        var failureOutcomes = new (AuditOutcome Outcome, string ActionCode, string ErrorCode, string Description)[]
        {
            (AuditOutcome.Denied, AuditAction.Delete.Code, "AUTHZ_INSUFFICIENT_ROLE", "Delete action rejected due to insufficient role permissions"),
            (AuditOutcome.Failure, "ProcessPayroll", "PAYMENT_GATEWAY_TIMEOUT", "Technical failure in payment gateway communication"),
            (AuditOutcome.Cancelled, "BatchReportGeneration", "USER_REQUEST_CANCELLED", "Operation explicitly aborted by user request"),
            (AuditOutcome.Partial, "BulkUserImport", "PARTIAL_CSV_VALIDATION_ERR", "Import completed with invalid rows skipped")
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
        Console.WriteLine("\n✓ ErrorCode Security Invariant: Must never store raw exception messages,");
        Console.WriteLine("  database connection strings, or stack traces, preventing forensic information disclosure.\n");
        Console.ResetColor();

        // ── 3. Active Resilient Decorator Enforcement (FailOpen vs Critical Actions) ──
        Console.WriteLine("3. Active Resilient Decorator Enforcement (ResilientAuditStoreDecorator):");
        var failingStore = new FailingAuditStore();
        var resilientConfig = new AuditConfiguration
        {
            DefaultFailureBehavior = AuditFailureBehavior.FailOpen
        };
        resilientConfig.CriticalActionCodes.Add("ProcessPayroll");

        var resilientStore = new ResilientAuditStoreDecorator(failingStore, resilientConfig);

        // Scenario A: Non-critical action with FailOpen -> Exception swallowed!
        var nonCriticalRecord = new AuditRecord
        {
            Id = AuditId.NewId(),
            OccurredAt = DateTimeOffset.UtcNow,
            Actor = AuditActor.System,
            Action = AuditAction.Read,
            Resource = new AuditResource("ProductCatalog", "cat-1"),
            Outcome = AuditOutcome.Success,
            Context = new AuditContext("tenant-a", "Storefront")
        };

        try
        {
            await resilientStore.AppendAsync(nonCriticalRecord);
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("✓ FailOpen Policy: Non-critical action ('Read') failure was successfully intercepted and swallowed.");
            Console.ResetColor();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Unexpected failure in FailOpen: {ex.Message}");
        }

        // Scenario B: Critical action ('ProcessPayroll') with FailOpen -> Must rethrow to protect business!
        var criticalRecord = new AuditRecord
        {
            Id = AuditId.NewId(),
            OccurredAt = DateTimeOffset.UtcNow,
            Actor = AuditActor.System,
            Action = new AuditAction("ProcessPayroll"),
            Resource = new AuditResource("PayrollBatch", "pb-2026-09"),
            Outcome = AuditOutcome.Success,
            Context = new AuditContext("tenant-a", "PayrollEngine")
        };

        try
        {
            await resilientStore.AppendAsync(criticalRecord);
            Console.WriteLine("✗ Critical action failure was swallowed unexpectedly!");
        }
        catch (InvalidOperationException ex)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"✓ CriticalAction Protection: Critical action ('ProcessPayroll') rethrew exception as expected: '{ex.Message}'.\n");
            Console.ResetColor();
        }
    }

    private sealed class FailingAuditStore : IAuditStore
    {
        public ValueTask AppendAsync(AuditRecord record, System.Threading.CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Simulated database timeout or connectivity failure.");

        public ValueTask AppendBatchAsync(System.Collections.Generic.IReadOnlyList<AuditRecord> records, System.Threading.CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Simulated database batch failure.");

        public ValueTask<AuditQueryResult> QueryAsync(AuditQuery query, System.Threading.CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Simulated database query failure.");
    }
}
