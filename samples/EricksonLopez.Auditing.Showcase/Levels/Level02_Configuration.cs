// Copyright © Erickson Lopez. MIT License.
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using EricksonLopez.Auditing.Testing;

namespace EricksonLopez.Auditing.Showcase.Levels;

/// <summary>
/// Level 2 — Full Configuration: Runtime Options, Security Policies, Batching & AuditFieldSensitivity
/// </summary>
public static class Level02_Configuration
{
    public static Task RunAsync()
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("\n================================================================================");
        Console.WriteLine(" [LEVEL 2] — FULL CONFIGURATION & RUNTIME POLICIES");
        Console.WriteLine("================================================================================\n");
        Console.ResetColor();

        var services = new ServiceCollection();

        // Comprehensive audit pipeline configuration
        services.AddAuditing(cfg =>
        {
            // 1. Failure semantics for storage provider (FailClosed, FailOpen, Deferred)
            cfg.DefaultFailureBehavior = AuditFailureBehavior.FailClosed;

            // 2. Critical actions that must ALWAYS be FailClosed
            cfg.CriticalActionCodes.Add("ProcessWireTransfer");
            cfg.CriticalActionCodes.Add("RevokeAdminPrivileges");

            // 3. Global denylist of sensitive fields (automatically excluded from changes)
            cfg.GlobalFieldDenylist.Add("CustomerCreditScore");
            cfg.GlobalFieldDenylist.Add("PaymentCardTrack2");

            // 4. Batching pipeline settings
            cfg.BatchChannelCapacity = 5000;
            cfg.BatchSize = 250;
            cfg.BatchFlushInterval = TimeSpan.FromSeconds(3);

            // 5. HMAC integrity chain — requires registered IAuditIntegrityProvider
            // cfg.EnableIntegrityChain = true; // (enabled in Level10)
        })
        .UseStore<InMemoryAuditStore>();

        var serviceProvider = services.BuildServiceProvider();
        var config = serviceProvider.GetRequiredService<AuditConfiguration>();

        Console.WriteLine("✓ Audit Configuration loaded and injected:");
        Console.WriteLine($"  • DefaultFailureBehavior: {config.DefaultFailureBehavior}");
        Console.WriteLine($"  • BatchChannelCapacity: {config.BatchChannelCapacity} records");
        Console.WriteLine($"  • BatchSize: {config.BatchSize} records per batch");
        Console.WriteLine($"  • BatchFlushInterval: {config.BatchFlushInterval.TotalSeconds}s");
        Console.WriteLine($"  • Critical Registered Actions ({config.CriticalActionCodes.Count}):");
        foreach (var action in config.CriticalActionCodes)
        {
            Console.WriteLine($"    - {action}");
        }

        Console.WriteLine($"\n  • Global Sensitive Field Denylist ({config.GlobalFieldDenylist.Count} protected fields):");
        int count = 0;
        foreach (var field in config.GlobalFieldDenylist)
        {
            Console.Write($"    {field,-20}");
            if (++count % 3 == 0) Console.WriteLine();
        }
        if (count % 3 != 0) Console.WriteLine();

        // ── AuditFieldSensitivity — individual field policy ─────────────────────
        Console.WriteLine("\n── AuditFieldSensitivity — Field Treatment Policies ──");
        Console.WriteLine("   Defines how sensitive field values are treated in AuditChange:");
        Console.WriteLine();
        Console.WriteLine($"   AuditFieldSensitivity.Include  ({(int)AuditFieldSensitivity.Include})  — Field is recorded normally (default behavior).");
        Console.WriteLine($"   AuditFieldSensitivity.Exclude  ({(int)AuditFieldSensitivity.Exclude})  — Field is COMPLETELY excluded from audit changes.");
        Console.WriteLine($"   AuditFieldSensitivity.Redact   ({(int)AuditFieldSensitivity.Redact})  — Field name preserved, value replaced with marker.");
        Console.WriteLine($"   AuditFieldSensitivity.Hash     ({(int)AuditFieldSensitivity.Hash})  — Value replaced with SHA-256 digest (enables equality check).");
        Console.WriteLine();
        Console.WriteLine("   Note: AuditFieldSensitivity defines the POLICY. Its execution occurs");
        Console.WriteLine("   via AuditSensitivityPipeline (see Level10) or explicit changes constructed");
        Console.WriteLine("   via AuditChange.Redacted() or AuditSensitivityPipeline.HashValue().\n");

        // ── Practical Demonstration of Apply / HashValue ────────────────────────
        Console.WriteLine("── Demonstration of AuditSensitivityPipeline with the configuration above ──");
        var sensitivityPipeline = serviceProvider.GetRequiredService<AuditSensitivityPipeline>();

        var rawChanges = new[]
        {
            new AuditChange("EmailAddress", "old@domain.com", "new@domain.com"),        // Included normally
            new AuditChange("Password", "hash_old_a1b2", "hash_new_c3d4"),              // Denylist → EXCLUDED
            new AuditChange("CustomerCreditScore", "720", "680"),                       // Custom denylist → EXCLUDED
            AuditChange.Redacted("SessionToken"),                                       // Explicit redaction → PRESERVED with IsRedacted=true
            new AuditChange("ProfileHash", null, AuditSensitivityPipeline.HashValue("alice@enterprise.com")) // SHA-256 Hash
        };

        var sanitized = sensitivityPipeline.Apply(rawChanges);
        Console.WriteLine($"  Original changes:  {rawChanges.Length} fields");
        Console.WriteLine($"  Sanitized changes: {sanitized?.Count ?? 0} fields");
        if (sanitized is not null)
        {
            foreach (var ch in sanitized)
            {
                Console.WriteLine($"    Field: {ch.Field,-20} | Redacted: {ch.IsRedacted} | New: {(ch.NewValue?.Length > 20 ? ch.NewValue[..20] + "..." : ch.NewValue) ?? "null"}");
            }
        }
        Console.WriteLine();

        return Task.CompletedTask;
    }
}
