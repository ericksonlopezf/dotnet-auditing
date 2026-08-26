// Copyright © Erickson Lopez. MIT License.
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using EricksonLopez.Auditing.Testing;

namespace EricksonLopez.Auditing.Showcase.Levels;

/// <summary>
/// Nivel 10 — Arquitectura Empresarial: Integridad HMAC Criptográfica, Detección Forense
/// de Manipulación, Protección GDPR, AuditRecordBuilder y TestAuditIntegrityProvider.
/// </summary>
public static class Level10_EnterpriseArchitecture
{
    public static async Task RunAsync()
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("\n================================================================================");
        Console.WriteLine(" [NIVEL 10] — ARQUITECTURA EMPRESARIAL: HMAC, GDPR Y TESTING HELPERS");
        Console.WriteLine("================================================================================\n");
        Console.ResetColor();

        // ── 1. Pipeline de Filtrado y Redacción de Datos Sensibles (GDPR / PCI-DSS)
        Console.WriteLine("── 1. AuditSensitivityPipeline — Zero-Leakage GDPR / PCI-DSS ──");
        var config = new AuditConfiguration();
        config.GlobalFieldDenylist.Add("InternalTaxSecret");

        var sensitivityPipeline = new AuditSensitivityPipeline(config);

        var rawChanges = new List<AuditChange>
        {
            new("Username", "old_alice", "new_alice"),
            new("Password", "secret123", "secret456"),             // Denylist global → EXCLUIDO
            new("CreditCardNumber", "4111-2222-3333-4444", null),  // Denylist global → EXCLUIDO
            new("ApiKey", "api_live_xyz", "api_live_abc"),         // Denylist global → EXCLUIDO
            AuditChange.Redacted("MedicalRecordData"),             // Redacción explícita → IsRedacted=true
            new("EmailHash", null, AuditSensitivityPipeline.HashValue("alice@enterprise.com")) // SHA-256
        };

        var sanitizedChanges = sensitivityPipeline.Apply(rawChanges);

        Console.WriteLine($"   • Cambios Originales:  {rawChanges.Count} campos");
        Console.WriteLine($"   • Cambios Sanitizados: {sanitizedChanges?.Count ?? 0} campos\n");

        if (sanitizedChanges != null)
        {
            foreach (var change in sanitizedChanges)
            {
                Console.WriteLine($"     - Campo: {change.Field,-20} | Old: {change.OldValue ?? "null",-15} | New: {(change.NewValue?.Length > 24 ? change.NewValue[..24] + "..." : change.NewValue) ?? "null",-28} | Redacted: {change.IsRedacted}");
            }
        }

        // ── 2. AuditRecordBuilder — Fluent Test Data Builder ─────────────────────
        Console.WriteLine("\n── 2. AuditRecordBuilder — Construcción Fluida de Registros (Testing) ──");
        Console.WriteLine("   AuditRecordBuilder del paquete Testing facilita construcción de registros");
        Console.WriteLine("   con defaults razonables y encadenamiento de métodos para tests unitarios.");
        Console.WriteLine();

        // Uso completo del fluent builder con todos los métodos de configuración
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
            .AddRedactedChange("ApproverSignatureData")  // AddRedactedChange() — helper para IsRedacted
            .WithErrorCode(null)  // Sin error en operación exitosa
            .Build();

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"✓ AuditRecord construido con AuditRecordBuilder:");
        Console.ResetColor();
        Console.WriteLine($"  • Id: {builtRecord.Id}");
        Console.WriteLine($"  • Actor: {builtRecord.Actor.DisplayName} ({builtRecord.Actor.Id})");
        Console.WriteLine($"  • Action: {builtRecord.Action.Code}");
        Console.WriteLine($"  • Resource: {builtRecord.Resource.Type}/{builtRecord.Resource.Id}");
        Console.WriteLine($"  • AggregateRoot: {builtRecord.Resource.AggregateType}/{builtRecord.Resource.AggregateId}");
        Console.WriteLine($"  • Tenant: {builtRecord.Context.TenantId}");
        Console.WriteLine($"  • CausationId: {builtRecord.Context.CausationId}");
        Console.WriteLine($"  • Changes: {builtRecord.Changes?.Count ?? 0} (incluyendo 1 redactado)");

        // AuditRecordBuilder.BuildDefault() — factory estática con overrides rápidos
        Console.WriteLine();
        var quickRecord = AuditRecordBuilder.BuildDefault(
            tenantId: "tenant-a",
            actorId: "usr-quick-01",
            resourceType: "Invoice",
            resourceId: "inv-99",
            outcome: AuditOutcome.Success,
            correlationId: "corr-quick");
        Console.WriteLine($"✓ AuditRecordBuilder.BuildDefault() — Actor: {quickRecord.Actor.Id}, Resource: {quickRecord.Resource.Type}/{quickRecord.Resource.Id}");

        // ── 3. TestAuditIntegrityProvider — Proveedor de Claves para Tests ────────
        Console.WriteLine("\n── 3. TestAuditIntegrityProvider — Claves HMAC para Tests ──");
        Console.WriteLine($"   • DefaultKey (32 bytes): [{string.Join(",", TestAuditIntegrityProvider.DefaultKey[..6])}...]");

        // SetTenantKey() — configurar clave diferente por tenant
        var testProvider = new TestAuditIntegrityProvider();
        var customTenantKey = new byte[32];
        for (int i = 0; i < 32; i++) customTenantKey[i] = (byte)(0xFF - i);

        testProvider.SetTenantKey("tenant-compliance-vault", customTenantKey);
        Console.WriteLine($"✓ SetTenantKey(\"tenant-compliance-vault\", customKey) — clave derivada configurada.");
        Console.WriteLine($"  La clave del tenant-compliance-vault es diferente de DefaultKey.");
        Console.WriteLine($"  • GetCurrentKey(\"tenant-compliance-vault\") devuelve la clave custom.");
        Console.WriteLine($"  • GetCurrentKey(\"otro-tenant\") devuelve DefaultKey.");

        // Verificar que efectivamente retorna claves distintas por tenant
        var vaultKey = testProvider.GetCurrentKey("tenant-compliance-vault");
        var defaultKey = testProvider.GetCurrentKey("tenant-generic");
        Console.WriteLine($"  • vault key[0] = {vaultKey.Span[0]}, default key[0] = {defaultKey.Span[0]} (distintos: {vaultKey.Span[0] != defaultKey.Span[0]})\n");

        // ── 4. Cadena Criptográfica HMAC-SHA256 y Detección de Manipulación ──────
        Console.WriteLine("── 4. Encadenamiento Criptográfico HMAC-SHA256 (Audit Hash Chain) ──");
        var hmacService = new HmacAuditIntegrityService(testProvider);
        const string tenantId = "tenant-compliance-vault";

        // Registro 1 (Génesis) — usando el builtRecord del builder
        var record1 = builtRecord; // Ya tiene tenantId = "tenant-compliance-vault"
        var hash1 = hmacService.ComputeHash(record1, previousHash: null);
        record1 = record1 with { IntegrityHash = hash1 };
        Console.WriteLine($"   [GENESIS 1] ID: {record1.Id} | Hash: {record1.IntegrityHash}");

        // Registro 2 (Encadenado al Registro 1)
        var record2 = AuditRecordBuilder.Create()
            .WithId(AuditId.NewId())
            .WithOccurredAt(DateTimeOffset.UtcNow.AddMinutes(-1))
            .WithActor(AuditActorType.User, "usr-sec-02", "Security Auditor")
            .WithAction(AuditAction.Export)
            .WithResource("AuditExport", "export-992")
            .WithOutcome(AuditOutcome.Success)
            .WithTenant(tenantId)
            .WithSource("CompliancePortal")
            .WithPreviousHash(record1.IntegrityHash)  // Enlace explícito al hash anterior
            .Build();

        var hash2 = hmacService.ComputeHash(record2, previousHash: record1.IntegrityHash);
        record2 = record2 with { IntegrityHash = hash2 };
        Console.WriteLine($"   [BLOCK   2] ID: {record2.Id} | PrevHash: {record2.PreviousHash![..16]}... | Hash: {record2.IntegrityHash}");

        // Verificación de integridad legítima
        bool valid1 = hmacService.Verify(record1);
        bool valid2 = hmacService.Verify(record2);
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"\n✓ Verificación de Registros Legítimos: R1={valid1}, R2={valid2}");
        Console.ResetColor();

        // ── 5. Detección Forense de Manipulación ─────────────────────────────────
        Console.WriteLine("\n── 5. Detección Forense de Manipulación de Evidencia (Tampering Attack) ──");
        Console.WriteLine("   Simulación: un atacante con acceso a la BD altera el ResourceId del Registro 1...");

        var tamperedRecord1 = record1 with
        {
            Resource = new AuditResource("Contract", "fraudulent-fund-99") // ResourceId alterado
        };

        bool tamperedVerify = hmacService.Verify(tamperedRecord1);
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"✗ Verificación del Registro Alterado: Válido = {tamperedVerify} (¡MANIPULACIÓN DETECTADA!)");
        Console.ResetColor();

        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("\n✓ Invariante Criptográfica: Ningún registro en la cadena puede ser modificado,");
        Console.WriteLine("  eliminado o reordenado sin invalidar matemáticamente la firma HMAC de toda la cadena.");
        Console.WriteLine();
        Console.WriteLine("✓ Para verificación de cadena sobre una base de datos real, use:");
        Console.WriteLine("  • PostgreSqlAuditIntegrityVerifier (IAuditIntegrityVerifier)");
        Console.WriteLine("  • SqlServerAuditIntegrityVerifier (IAuditIntegrityVerifier)");
        Console.WriteLine("  • SqliteAuditIntegrityVerifier   (IAuditIntegrityVerifier, demostrado en Level09)");
        Console.WriteLine("  • MySqlAuditIntegrityVerifier    (IAuditIntegrityVerifier)");
        Console.WriteLine("  • OracleAuditIntegrityVerifier   (IAuditIntegrityVerifier)\n");
        Console.ResetColor();

        await Task.CompletedTask;
    }
}
