// Copyright © Erickson Lopez. MIT License.
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using EricksonLopez.Auditing.Testing;

namespace EricksonLopez.Auditing.Showcase.Levels;

/// <summary>
/// Level 10 — Enterprise Architecture: Cryptographic HMAC Integrity, Forensic Tamper Detection,
/// GDPR Protection, AuditRecordBuilder & TestAuditIntegrityProvider.
/// </summary>
public static class Level10_EnterpriseArchitecture
{
    public static async Task RunAsync()
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("\n================================================================================");
        Console.WriteLine(" [LEVEL 10] — ENTERPRISE ARCHITECTURE: HMAC, GDPR & TESTING HELPERS");
        Console.WriteLine("================================================================================\n");
        Console.ResetColor();

        // ── 1. Sensitive Data Redaction Pipeline (GDPR / PCI-DSS) ────────────────
        Console.WriteLine("── 1. AuditSensitivityPipeline — Zero-Leakage GDPR / PCI-DSS ──");
        var config = new AuditConfiguration();
        config.GlobalFieldDenylist.Add("InternalTaxSecret");

        var sensitivityPipeline = new AuditSensitivityPipeline(config);

        var rawChanges = new List<AuditChange>
        {
            new("Username", "old_alice", "new_alice"),
            new("Password", "secret123", "secret456"),             // Global denylist → EXCLUDED
            new("CreditCardNumber", "4111-2222-3333-4444", null),  // Global denylist → EXCLUDED
            new("ApiKey", "api_live_xyz", "api_live_abc"),         // Global denylist → EXCLUDED
            AuditChange.Redacted("MedicalRecordData"),             // Explicit redaction → IsRedacted=true
            new("EmailHash", null, AuditSensitivityPipeline.HashValue("alice@enterprise.com")) // SHA-256
        };

        var sanitizedChanges = sensitivityPipeline.Apply(rawChanges);

        Console.WriteLine($"   • Original Changes:  {rawChanges.Count} fields");
        Console.WriteLine($"   • Sanitized Changes: {sanitizedChanges?.Count ?? 0} fields\n");

        if (sanitizedChanges != null)
        {
            foreach (var change in sanitizedChanges)
            {
                Console.WriteLine($"     - Field: {change.Field,-20} | Old: {change.OldValue ?? "null",-15} | New: {(change.NewValue?.Length > 24 ? change.NewValue[..24] + "..." : change.NewValue) ?? "null",-28} | Redacted: {change.IsRedacted}");
            }
        }

        // ── 2. AuditRecordBuilder — Fluent Test Data Builder ─────────────────────
        Console.WriteLine("\n── 2. AuditRecordBuilder — Fluent Record Construction (Testing) ──");
        Console.WriteLine("   AuditRecordBuilder in the Testing package enables fluent construction");
        Console.WriteLine("   of complete audit records with sensible defaults for unit tests.");
        Console.WriteLine();

        var builtRecord = AuditRecordBuilder.Create()
            .WithId(AuditId.NewId())
            .WithOccurredAt(DateTimeOffset.UtcNow.AddMinutes(-2))
            .WithActor(AuditActorType.User, "usr-builder-001", "Builder Admin")
            .WithAction(AuditAction.Approve)
            .WithResource("Contract", "contract-99812", aggregateType: "Client", aggregateId: "client-5501")
            .WithOutcome(AuditOutcome.Success)
            .WithTenant("tenant-compliance-vault")
            .WithSource("ContractApprovalService")
            .WithCorrelationId("corr-contract-approval-99")
            .WithCausationId("cmd-approve-contract-99812")
            .WithRequestId("http-req-77210")
            .WithIpAddress("10.10.10.20")
            .WithUserAgent("MyApp/1.0.0 (Compliance Module)")
            .AddChange("Status", "Draft", "Approved")
            .AddChange("ReviewedBy", null, "usr-builder-001")
            .AddRedactedChange("ApproverSignatureData")
            .WithErrorCode(null)
            .Build();

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"✓ AuditRecord built with AuditRecordBuilder:");
        Console.ResetColor();
        Console.WriteLine($"  • Id: {builtRecord.Id}");
        Console.WriteLine($"  • Actor: {builtRecord.Actor.DisplayName} ({builtRecord.Actor.Id})");
        Console.WriteLine($"  • Action: {builtRecord.Action.Code}");
        Console.WriteLine($"  • Resource: {builtRecord.Resource.Type}/{builtRecord.Resource.Id}");
        Console.WriteLine($"  • AggregateRoot: {builtRecord.Resource.AggregateType}/{builtRecord.Resource.AggregateId}");
        Console.WriteLine($"  • Tenant: {builtRecord.Context.TenantId}");
        Console.WriteLine($"  • CausationId: {builtRecord.Context.CausationId}");
        Console.WriteLine($"  • Changes: {builtRecord.Changes?.Count ?? 0} (including 1 redacted)");

        Console.WriteLine();
        var quickRecord = AuditRecordBuilder.BuildDefault(
            tenantId: "tenant-a",
            actorId: "usr-quick-01",
            resourceType: "Invoice",
            resourceId: "inv-99",
            outcome: AuditOutcome.Success,
            correlationId: "corr-quick");
        Console.WriteLine($"✓ AuditRecordBuilder.BuildDefault() — Actor: {quickRecord.Actor.Id}, Resource: {quickRecord.Resource.Type}/{quickRecord.Resource.Id}");

        // ── 3. TestAuditIntegrityProvider — Key Provider for Tests ───────────────
        Console.WriteLine("\n── 3. TestAuditIntegrityProvider — HMAC Key Provider for Tests ──");
        Console.WriteLine($"   • DefaultKey (32 bytes): [{string.Join(",", TestAuditIntegrityProvider.DefaultKey[..6])}...]");

        var testProvider = new TestAuditIntegrityProvider();
        var customTenantKey = new byte[32];
        for (int i = 0; i < 32; i++) customTenantKey[i] = (byte)(0xFF - i);

        testProvider.SetTenantKey("tenant-compliance-vault", customTenantKey);
        Console.WriteLine($"✓ SetTenantKey(\"tenant-compliance-vault\", customKey) — Derived tenant key configured.");
        Console.WriteLine($"  Key for tenant-compliance-vault is distinct from DefaultKey.");
        Console.WriteLine($"  • GetCurrentKey(\"tenant-compliance-vault\") returns custom key.");
        Console.WriteLine($"  • GetCurrentKey(\"generic-tenant\") returns DefaultKey.");

        var vaultKey = testProvider.GetCurrentKey("tenant-compliance-vault");
        var defaultKey = testProvider.GetCurrentKey("tenant-generic");
        Console.WriteLine($"  • vault key[0] = {vaultKey.Span[0]}, default key[0] = {defaultKey.Span[0]} (distinct: {vaultKey.Span[0] != defaultKey.Span[0]})\n");

        // ── 4. Cryptographic HMAC-SHA256 Chaining & Tamper Detection ─────────────
        Console.WriteLine("── 4. Cryptographic HMAC-SHA256 Chaining (Audit Hash Chain) ──");
        var hmacService = new HmacAuditIntegrityService(testProvider);
        const string tenantId = "tenant-compliance-vault";

        // Record 1 (Genesis)
        var record1 = builtRecord;
        var hash1 = hmacService.ComputeHash(record1, previousHash: null);
        record1 = record1 with { IntegrityHash = hash1 };
        Console.WriteLine($"   [GENESIS 1] ID: {record1.Id} | Hash: {record1.IntegrityHash}");

        // Record 2 (Chained to Record 1)
        var record2 = AuditRecordBuilder.Create()
            .WithId(AuditId.NewId())
            .WithOccurredAt(DateTimeOffset.UtcNow.AddMinutes(-1))
            .WithActor(AuditActorType.User, "usr-sec-02", "Security Auditor")
            .WithAction(AuditAction.Export)
            .WithResource("AuditExport", "export-992")
            .WithOutcome(AuditOutcome.Success)
            .WithTenant(tenantId)
            .WithSource("CompliancePortal")
            .WithPreviousHash(record1.IntegrityHash)
            .Build();

        var hash2 = hmacService.ComputeHash(record2, previousHash: record1.IntegrityHash);
        record2 = record2 with { IntegrityHash = hash2 };
        Console.WriteLine($"   [BLOCK   2] ID: {record2.Id} | PrevHash: {record2.PreviousHash![..16]}... | Hash: {record2.IntegrityHash}");

        // Valid integrity verification
        bool valid1 = hmacService.Verify(record1);
        bool valid2 = hmacService.Verify(record2);
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"\n✓ Verification of Legitimate Records: R1={valid1}, R2={valid2}");
        Console.ResetColor();

        // ── 5. Forensic Tamper Detection ─────────────────────────────────────────
        Console.WriteLine("\n── 5. Forensic Tamper Attack Detection ──");
        Console.WriteLine("   Simulation: Database adversary alters ResourceId of Record 1...");

        var tamperedRecord1 = record1 with
        {
            Resource = new AuditResource("Contract", "fraudulent-fund-99")
        };

        bool tamperedVerify = hmacService.Verify(tamperedRecord1);
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"✗ Verification of Altered Record: Valid = {tamperedVerify} (TAMPERING DETECTED!)");
        Console.ResetColor();

        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("\n✓ Cryptographic Invariant: No record in the chain can be modified,");
        Console.WriteLine("  deleted, or reordered without mathematically breaking the HMAC signature of the entire chain.");
        Console.WriteLine();
        Console.WriteLine("✓ For database chain verification, use:");
        Console.WriteLine("  • PostgreSqlAuditIntegrityVerifier (IAuditIntegrityVerifier)");
        Console.WriteLine("  • SqlServerAuditIntegrityVerifier (IAuditIntegrityVerifier)");
        Console.WriteLine("  • SqliteAuditIntegrityVerifier   (IAuditIntegrityVerifier, demonstrated in Level09)");
        Console.WriteLine("  • MySqlAuditIntegrityVerifier    (IAuditIntegrityVerifier)");
        Console.WriteLine("  • OracleAuditIntegrityVerifier   (IAuditIntegrityVerifier)\n");
        Console.ResetColor();

        await Task.CompletedTask;
    }
}
