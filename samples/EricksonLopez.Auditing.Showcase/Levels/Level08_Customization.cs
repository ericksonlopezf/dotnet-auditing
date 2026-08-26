// Copyright © Erickson Lopez. MIT License.
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace EricksonLopez.Auditing.Showcase.Levels;

/// <summary>
/// Proveedor de Actor Personalizado basado en Claims de Identidad.
/// Implementación de referencia de IAuditActorProvider para apps web con JWT.
/// </summary>
public sealed class ShowcaseClaimsAuditActorProvider : IAuditActorProvider
{
    private readonly string _simulatedUserId;
    private readonly string _simulatedDisplayName;

    public ShowcaseClaimsAuditActorProvider()
    {
        _simulatedUserId = "usr-jwt-claims-1049";
        _simulatedDisplayName = "Carol Smith (SecOps Admin)";
    }

    public AuditActor GetCurrentActor()
    {
        // En una app web real, se extraería de IHttpContextAccessor -> HttpContext.User
        return new AuditActor(AuditActorType.User, _simulatedUserId, _simulatedDisplayName);
    }
}

/// <summary>
/// Proveedor de Contexto de Auditoría Personalizado.
/// Implementación de referencia de IAuditContextProvider para enriquecimiento ambiental
/// de TenantId, CorrelationId y Source desde el contexto de ejecución.
/// </summary>
public sealed class ShowcaseAmbientContextProvider : IAuditContextProvider
{
    // En una app real, estos valores vendrían de:
    // - IHttpContextAccessor para TenantId (claim del JWT)
    // - ICorrelationService / Activity.Current?.TraceId para CorrelationId
    // - ApplicationName desde IConfiguration para Source
    private readonly string _tenantId;
    private readonly string _source;
    private readonly string? _correlationId;

    public ShowcaseAmbientContextProvider(string tenantId, string source, string? correlationId = null)
    {
        _tenantId = tenantId;
        _source = source;
        _correlationId = correlationId;
    }

    public AuditContext GetCurrentContext() =>
        new AuditContext(
            TenantId: _tenantId,
            Source: _source,
            CorrelationId: _correlationId);
}

/// <summary>
/// Proveedor de Claves Criptográficas HMAC simulando un Key Management System (AWS KMS / Azure Key Vault)
/// </summary>
public sealed class ShowcaseKmsAuditIntegrityProvider : IAuditIntegrityProvider
{
    // Clave de 32 bytes (256 bits) para HMAC-SHA256
    private static readonly byte[] _masterKmsKey = new byte[]
    {
        0x1A, 0x2B, 0x3C, 0x4D, 0x5E, 0x6F, 0x70, 0x81,
        0x92, 0xA3, 0xB4, 0xC5, 0xD6, 0xE7, 0xF8, 0x09,
        0x10, 0x21, 0x32, 0x43, 0x54, 0x65, 0x76, 0x87,
        0x98, 0xA9, 0xBA, 0xCB, 0xDC, 0xED, 0xFE, 0x0F
    };

    public ReadOnlyMemory<byte> GetCurrentKey(string tenantId)
    {
        // En producción: derivar o recuperar la clave del KMS específica para el tenant
        return _masterKmsKey;
    }
}

/// <summary>
/// Implementación Personalizada de IAuditStore orientada a SIEM / Logging de Seguridad
/// </summary>
public sealed class ShowcaseSiemAuditStore : IAuditStore
{
    private readonly List<AuditRecord> _siemBuffer = new();

    public ValueTask AppendAsync(AuditRecord record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        _siemBuffer.Add(record);
        Console.WriteLine($"   [SIEM FORWARDER] » Evento emitido: {record.Action.Code} por {record.Actor.DisplayName} ({record.Actor.Id})");
        return ValueTask.CompletedTask;
    }

    public ValueTask AppendBatchAsync(IReadOnlyList<AuditRecord> records, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(records);
        foreach (var r in records)
        {
            _siemBuffer.Add(r);
        }
        Console.WriteLine($"   [SIEM FORWARDER] » Batch de {records.Count} eventos emitidos a SIEM.");
        return ValueTask.CompletedTask;
    }

    public ValueTask<AuditQueryResult> QueryAsync(AuditQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        return ValueTask.FromResult(new AuditQueryResult(_siemBuffer, null, false));
    }
}

/// <summary>
/// Nivel 8 — Personalización: Sustitución de Componentes Públicos e Inyección.
/// Demuestra IAuditActorProvider, IAuditContextProvider, IAuditIntegrityProvider,
/// IAuditStore, SystemAuditActorProvider e IAuditBuilder.
/// </summary>
public static class Level08_Customization
{
    public static async Task RunAsync()
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("\n================================================================================");
        Console.WriteLine(" [NIVEL 8] — EXTENSIBILIDAD Y PERSONALIZACIÓN DE INTERFACES PÚBLICAS");
        Console.WriteLine("================================================================================\n");
        Console.ResetColor();

        // ── 1. SystemAuditActorProvider — singleton predefinido ──────────────────
        Console.WriteLine("1. SystemAuditActorProvider — Proveedor Predefinido de Sistema:");
        Console.WriteLine("   Registrado automáticamente como default en AddAuditing() cuando no se");
        Console.WriteLine("   especifica otro proveedor. Retorna AuditActor.System para backgrounds.");
        var systemProvider = SystemAuditActorProvider.Instance;
        var systemActor = systemProvider.GetCurrentActor();
        Console.WriteLine($"   • SystemAuditActorProvider.Instance → Actor: {systemActor.Id} [{systemActor.Type}]");
        Console.WriteLine($"   • Equivalente a: AuditActor.System = ({AuditActor.System.Type}, \"{AuditActor.System.Id}\")\n");

        // ── 2. Registro de proveedores custom mediante IAuditBuilder ────────────
        Console.WriteLine("2. IAuditBuilder — Registro de Proveedores Personalizados:");
        var services = new ServiceCollection();

        services.AddAuditing()
                .UseActorProvider<ShowcaseClaimsAuditActorProvider>()  // IAuditActorProvider custom
                .EnableIntegrityChain()                                 // Activa HmacAuditIntegrityService
                .UseStore<ShowcaseSiemAuditStore>();                    // IAuditStore custom

        // IAuditIntegrityProvider — requerido cuando EnableIntegrityChain() está activo
        services.AddSingleton<IAuditIntegrityProvider, ShowcaseKmsAuditIntegrityProvider>();

        // IAuditContextProvider — proveedor ambiental de contexto (registro manual)
        services.AddSingleton<IAuditContextProvider>(
            new ShowcaseAmbientContextProvider(
                tenantId: "tenant-enterprise",
                source: "CloudControlPlane",
                correlationId: "corr-cloud-deploy-2026"));

        var sp = services.BuildServiceProvider();
        var actorProvider = sp.GetRequiredService<IAuditActorProvider>();
        var contextProvider = sp.GetRequiredService<IAuditContextProvider>();
        var auditStore = sp.GetRequiredService<IAuditStore>();
        var integrityService = sp.GetRequiredService<HmacAuditIntegrityService>();

        Console.WriteLine("✓ Componentes personalizados registrados e inyectados:");
        Console.WriteLine($"  • IAuditActorProvider: {actorProvider.GetType().Name}");
        Console.WriteLine($"  • IAuditContextProvider: {contextProvider.GetType().Name}");
        Console.WriteLine($"  • IAuditStore: {auditStore.GetType().Name}");
        Console.WriteLine($"  • HmacAuditIntegrityService: Configurado con {sp.GetRequiredService<IAuditIntegrityProvider>().GetType().Name}\n");

        // ── 3. IAuditContextProvider — resolución automática de contexto ─────────
        Console.WriteLine("3. IAuditContextProvider — Resolución Ambiental de Contexto:");
        var ambientContext = contextProvider.GetCurrentContext();
        Console.WriteLine($"   • TenantId resuelto: {ambientContext.TenantId}");
        Console.WriteLine($"   • Source resuelto: {ambientContext.Source}");
        Console.WriteLine($"   • CorrelationId ambiental: {ambientContext.CorrelationId}\n");

        // ── 4. IAuditActorProvider — resolución automática de actor ─────────────
        Console.WriteLine("4. IAuditActorProvider — Resolución de Actor desde Claims:");
        var currentActor = actorProvider.GetCurrentActor();
        Console.WriteLine($"   • Actor resuelto: {currentActor.DisplayName} ({currentActor.Id}) [{currentActor.Type}]\n");

        // ── 5. Crear y firmar criptográficamente el registro ─────────────────────
        Console.WriteLine("5. HmacAuditIntegrityService — Firma Criptográfica de Registro:");
        var record = new AuditRecord
        {
            Id = AuditId.NewId(),
            OccurredAt = DateTimeOffset.UtcNow,
            Actor = currentActor,
            Action = AuditAction.GrantPermission,
            Resource = new AuditResource("CloudResourceGroup", "rg-production-east"),
            Outcome = AuditOutcome.Success,
            Context = ambientContext     // Reutilizando contexto del proveedor ambiental
        };

        // Generar hash de integridad del primer registro (previousHash = null → génesis)
        var hash = integrityService.ComputeHash(record, previousHash: null);
        var recordWithHash = record with { IntegrityHash = hash };

        Console.WriteLine($"   • ID: {recordWithHash.Id}");
        Console.WriteLine($"   • Hash HMAC-SHA256: {recordWithHash.IntegrityHash}");

        // Verificar que el hash es correcto
        bool isValid = integrityService.Verify(recordWithHash);
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"   • Verificación: {(isValid ? "✓ Válido" : "✗ Inválido")}");
        Console.ResetColor();

        // ── 6. Persistir en el almacén SIEM personalizado ────────────────────────
        Console.WriteLine("\n6. Persistencia en IAuditStore personalizado (ShowcaseSiemAuditStore):");
        await auditStore.AppendAsync(recordWithHash);

        Console.WriteLine($"\n✓ Flujo completo: Actor resuelto → Contexto ambiental → Firma HMAC → Persistencia SIEM.\n");
    }
}
