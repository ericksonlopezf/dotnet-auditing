// Copyright © Erickson Lopez. MIT License.
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace EricksonLopez.Auditing.Showcase.Levels;

/// <summary>
/// Custom Actor Provider based on Identity Claims.
/// Reference implementation of IAuditActorProvider for web apps with JWT.
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
        // In a real web application, extracted from IHttpContextAccessor -> HttpContext.User
        return new AuditActor(AuditActorType.User, _simulatedUserId, _simulatedDisplayName);
    }
}

/// <summary>
/// Custom Audit Context Provider.
/// Reference implementation of IAuditContextProvider for ambient enrichment
/// of TenantId, CorrelationId, and Source from execution context.
/// </summary>
public sealed class ShowcaseAmbientContextProvider : IAuditContextProvider
{
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
/// HMAC Cryptographic Key Provider simulating a Key Management System (AWS KMS / Azure Key Vault).
/// </summary>
public sealed class ShowcaseKmsAuditIntegrityProvider : IAuditIntegrityProvider
{
    // 32-byte (256-bit) key for HMAC-SHA256
    private static readonly byte[] _masterKmsKey = new byte[]
    {
        0x1A, 0x2B, 0x3C, 0x4D, 0x5E, 0x6F, 0x70, 0x81,
        0x92, 0xA3, 0xB4, 0xC5, 0xD6, 0xE7, 0xF8, 0x09,
        0x10, 0x21, 0x32, 0x43, 0x54, 0x65, 0x76, 0x87,
        0x98, 0xA9, 0xBA, 0xCB, 0xDC, 0xED, 0xFE, 0x0F
    };

    public ReadOnlyMemory<byte> GetCurrentKey(string tenantId)
    {
        // In production: derive or fetch tenant-specific key from KMS
        return _masterKmsKey;
    }
}

/// <summary>
/// Custom IAuditStore Implementation for SIEM / Security Logging.
/// </summary>
public sealed class ShowcaseSiemAuditStore : IAuditStore
{
    private readonly List<AuditRecord> _siemBuffer = new();

    public ValueTask AppendAsync(AuditRecord record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        _siemBuffer.Add(record);
        Console.WriteLine($"   [SIEM FORWARDER] » Event emitted: {record.Action.Code} by {record.Actor.DisplayName} ({record.Actor.Id})");
        return ValueTask.CompletedTask;
    }

    public ValueTask AppendBatchAsync(IReadOnlyList<AuditRecord> records, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(records);
        foreach (var r in records)
        {
            _siemBuffer.Add(r);
        }
        Console.WriteLine($"   [SIEM FORWARDER] » Batch of {records.Count} events emitted to SIEM.");
        return ValueTask.CompletedTask;
    }

    public ValueTask<AuditQueryResult> QueryAsync(AuditQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        return ValueTask.FromResult(new AuditQueryResult(_siemBuffer, null, false));
    }
}

/// <summary>
/// Level 8 — Customization: Replacing Public Components & Dependency Injection.
/// Demonstrates IAuditActorProvider, IAuditContextProvider, IAuditIntegrityProvider,
/// IAuditStore, SystemAuditActorProvider, and IAuditBuilder.
/// </summary>
public static class Level08_Customization
{
    public static async Task RunAsync()
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("\n================================================================================");
        Console.WriteLine(" [LEVEL 8] — EXTENSIBILITY & CUSTOMIZATION OF PUBLIC INTERFACES");
        Console.WriteLine("================================================================================\n");
        Console.ResetColor();

        // ── 1. SystemAuditActorProvider — Predefined Singleton ───────────────────
        Console.WriteLine("1. SystemAuditActorProvider — Predefined System Provider:");
        Console.WriteLine("   Registered automatically as default in AddAuditing() when no");
        Console.WriteLine("   custom provider is specified. Returns AuditActor.System for background tasks.");
        var systemProvider = SystemAuditActorProvider.Instance;
        var systemActor = systemProvider.GetCurrentActor();
        Console.WriteLine($"   • SystemAuditActorProvider.Instance → Actor: {systemActor.Id} [{systemActor.Type}]");
        Console.WriteLine($"   • Equivalent to: AuditActor.System = ({AuditActor.System.Type}, \"{AuditActor.System.Id}\")\n");

        // ── 2. Registering Custom Providers via IAuditBuilder ────────────────────
        Console.WriteLine("2. IAuditBuilder — Registering Custom Providers:");
        var services = new ServiceCollection();

        services.AddAuditing()
                .UseActorProvider<ShowcaseClaimsAuditActorProvider>()  // Custom IAuditActorProvider
                .EnableIntegrityChain()                                 // Enables HmacAuditIntegrityService
                .UseStore<ShowcaseSiemAuditStore>();                    // Custom IAuditStore

        // IAuditIntegrityProvider — required when EnableIntegrityChain() is active
        services.AddSingleton<IAuditIntegrityProvider, ShowcaseKmsAuditIntegrityProvider>();

        // IAuditContextProvider — ambient context provider
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

        Console.WriteLine("✓ Custom components registered and injected:");
        Console.WriteLine($"  • IAuditActorProvider: {actorProvider.GetType().Name}");
        Console.WriteLine($"  • IAuditContextProvider: {contextProvider.GetType().Name}");
        Console.WriteLine($"  • IAuditStore: {auditStore.GetType().Name}");
        Console.WriteLine($"  • HmacAuditIntegrityService: Configured with {sp.GetRequiredService<IAuditIntegrityProvider>().GetType().Name}\n");

        // ── 3. IAuditContextProvider — Ambient Context Resolution ────────────────
        Console.WriteLine("3. IAuditContextProvider — Ambient Context Resolution:");
        var ambientContext = contextProvider.GetCurrentContext();
        Console.WriteLine($"   • Resolved TenantId: {ambientContext.TenantId}");
        Console.WriteLine($"   • Resolved Source: {ambientContext.Source}");
        Console.WriteLine($"   • Ambient CorrelationId: {ambientContext.CorrelationId}\n");

        // ── 4. IAuditActorProvider — Actor Resolution from Claims ────────────────
        Console.WriteLine("4. IAuditActorProvider — Actor Resolution from Claims:");
        var currentActor = actorProvider.GetCurrentActor();
        Console.WriteLine($"   • Resolved Actor: {currentActor.DisplayName} ({currentActor.Id}) [{currentActor.Type}]\n");

        // ── 5. Create & Cryptographically Sign Record ────────────────────────────
        Console.WriteLine("5. HmacAuditIntegrityService — Cryptographic Record Signing:");
        var record = new AuditRecord
        {
            Id = AuditId.NewId(),
            OccurredAt = DateTimeOffset.UtcNow,
            Actor = currentActor,
            Action = AuditAction.GrantPermission,
            Resource = new AuditResource("CloudResourceGroup", "rg-production-east"),
            Outcome = AuditOutcome.Success,
            Context = ambientContext
        };

        // Compute HMAC integrity hash (previousHash = null → Genesis record)
        var hash = integrityService.ComputeHash(record, previousHash: null);
        var recordWithHash = record with { IntegrityHash = hash };

        Console.WriteLine($"   • ID: {recordWithHash.Id}");
        Console.WriteLine($"   • HMAC-SHA256 Hash: {recordWithHash.IntegrityHash}");

        // Verify hash validity
        bool isValid = integrityService.Verify(recordWithHash);
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"   • Verification: {(isValid ? "✓ Valid" : "✗ Invalid")}");
        Console.ResetColor();

        // ── 6. Persist to Custom SIEM Store ──────────────────────────────────────
        Console.WriteLine("\n6. Persistence to Custom IAuditStore (ShowcaseSiemAuditStore):");
        await auditStore.AppendAsync(recordWithHash);

        Console.WriteLine($"\n✓ Complete Flow: Resolved Actor → Ambient Context → HMAC Signature → SIEM Persistence.\n");
    }
}
