// Copyright © Erickson Lopez. MIT License.
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using EricksonLopez.Auditing.Testing;

namespace EricksonLopez.Auditing.Showcase.Levels;

/// <summary>
/// Definición de Acciones de Negocio Personalizadas (AuditAction es extensible, no un enum cerrado)
/// </summary>
public static class CustomDomainActions
{
    public static readonly AuditAction ProcessPayment = new("ProcessPayment");
    public static readonly AuditAction ExportGdprData = new("ExportGdprData");
    public static readonly AuditAction AuthorizeRefund = new("AuthorizeRefund");
    public static readonly AuditAction BulkUserImport = new("BulkUserImport");
}

/// <summary>
/// Nivel 3 — Casos de Uso Reales: Escenarios de Negocio Derivados de la API Pública
/// Cubre todas las AuditAction predefinidas y patrones de negocio completos.
/// </summary>
public static class Level03_RealWorldUseCases
{
    public static async Task RunAsync()
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("\n================================================================================");
        Console.WriteLine(" [NIVEL 3] — CASOS DE USO REALES DEL MUNDO EMPRESARIAL");
        Console.WriteLine("================================================================================\n");
        Console.ResetColor();

        var services = new ServiceCollection();
        services.AddAuditing()
                .UseStore<InMemoryAuditStore>();

        var sp = services.BuildServiceProvider();
        var store = sp.GetRequiredService<IAuditStore>();

        // ── Escenario 1: Autenticación y Elevación de Privilegios ─────────────────
        Console.WriteLine("── Escenario 1: Autenticación y Concesión de Permisos ──");
        var loginRecord = new AuditRecord
        {
            Id = AuditId.NewId(),
            OccurredAt = DateTimeOffset.UtcNow,
            Actor = new AuditActor(AuditActorType.User, "usr-bob-102", "bob.admin@domain.com"),
            Action = AuditAction.Login,
            Resource = new AuditResource("Session", "sess-991823"),
            Outcome = AuditOutcome.Success,
            Context = new AuditContext(
                TenantId: "tenant-fintech",
                Source: "IdentityService",
                IpAddress: "192.168.1.100",
                UserAgent: "Mozilla/5.0 (Windows NT 10.0; Win64; x64)")
        };
        await store.AppendAsync(loginRecord);
        Console.WriteLine($"✓ [LOGIN] Usuario '{loginRecord.Actor.DisplayName}' inició sesión.");

        var grantPermRecord = new AuditRecord
        {
            Id = AuditId.NewId(),
            OccurredAt = DateTimeOffset.UtcNow,
            Actor = new AuditActor(AuditActorType.User, "usr-bob-102", "bob.admin@domain.com"),
            Action = AuditAction.GrantPermission,
            Resource = new AuditResource("RoleAssignment", "role-financial-officer"),
            Outcome = AuditOutcome.Success,
            Context = new AuditContext(
                TenantId: "tenant-fintech",
                Source: "AdminPortal",
                CorrelationId: "corr-perm-grant-01"),
            Changes = new[]
            {
                new AuditChange("TargetUser", null, "usr-alice-404"),
                new AuditChange("AssignedRole", null, "FinancialOfficer")
            }
        };
        await store.AppendAsync(grantPermRecord);
        Console.WriteLine($"✓ [GRANT_PERMISSION] Rol 'FinancialOfficer' otorgado a 'usr-alice-404'.\n");

        // ── Escenario 2: Modificación con Jerarquía de Agregados ──────────────────
        Console.WriteLine("── Escenario 2: Sub-entidad dentro de Aggregate Root ──");
        var orderItemRecord = new AuditRecord
        {
            Id = AuditId.NewId(),
            OccurredAt = DateTimeOffset.UtcNow,
            Actor = new AuditActor(AuditActorType.User, "usr-alice-404", "alice@domain.com"),
            Action = AuditAction.Update,
            Resource = new AuditResource(
                Type: "OrderItem",
                Id: "item-line-3",
                AggregateType: "Order",
                AggregateId: "order-2026-9901"),
            Outcome = AuditOutcome.Success,
            Context = new AuditContext(
                TenantId: "tenant-fintech",
                Source: "OrderManagementService"),
            Changes = new[]
            {
                new AuditChange("Quantity", "2", "5"),
                new AuditChange("UnitPrice", "49.99", "45.00"),
                AuditChange.Redacted("DiscountCouponSecret") // Campo redactado explícitamente
            }
        };
        await store.AppendAsync(orderItemRecord);
        Console.WriteLine($"✓ [UPDATE] Item '{orderItemRecord.Resource.Id}' en Orden raíz '{orderItemRecord.Resource.AggregateId}'.");
        Console.WriteLine($"  - Cambios registrados con redacción de secretos.\n");

        // ── Escenario 3: Acción personalizada y denegación ────────────────────────
        Console.WriteLine("── Escenario 3: Acción Personalizada y Denegación de Acceso ──");
        var deniedRecord = new AuditRecord
        {
            Id = AuditId.NewId(),
            OccurredAt = DateTimeOffset.UtcNow,
            Actor = new AuditActor(AuditActorType.User, "usr-mallory-666", "mallory@external.com"),
            Action = CustomDomainActions.AuthorizeRefund,
            Resource = new AuditResource("RefundRequest", "ref-5501"),
            Outcome = AuditOutcome.Denied,
            ErrorCode = "ERR_INSUFFICIENT_SECURITY_CLEARANCE",
            Context = new AuditContext(
                TenantId: "tenant-fintech",
                Source: "PaymentGatewayService")
        };
        await store.AppendAsync(deniedRecord);
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"⚠ [DENIED] Acción personalizada '{deniedRecord.Action.Code}' denegada con ErrorCode: '{deniedRecord.ErrorCode}'.");
        Console.ResetColor();
        Console.WriteLine();

        // ── Escenario 4: Acciones de Ciclo de Vida Predefinidas ───────────────────
        Console.WriteLine("── Escenario 4: Acciones Predefinidas — Download, Send, Cancel, Restore ──");

        // AuditAction.Download — descarga de recurso protegido
        var downloadRecord = new AuditRecord
        {
            Id = AuditId.NewId(),
            OccurredAt = DateTimeOffset.UtcNow,
            Actor = new AuditActor(AuditActorType.User, "usr-alice-404", "alice@domain.com"),
            Action = AuditAction.Download,
            Resource = new AuditResource("FinancialReport", "report-q1-2026.pdf"),
            Outcome = AuditOutcome.Success,
            Context = new AuditContext("tenant-fintech", "CompliancePortal")
        };
        await store.AppendAsync(downloadRecord);
        Console.WriteLine($"✓ [DOWNLOAD] Informe financiero descargado: '{downloadRecord.Resource.Id}'.");

        // AuditAction.Send — envío de notificación o documento
        var sendRecord = new AuditRecord
        {
            Id = AuditId.NewId(),
            OccurredAt = DateTimeOffset.UtcNow,
            Actor = new AuditActor(AuditActorType.Service, "notification-svc", "Notification Service"),
            Action = AuditAction.Send,
            Resource = new AuditResource("EmailNotification", "notif-8812"),
            Outcome = AuditOutcome.Success,
            Context = new AuditContext(
                TenantId: "tenant-fintech",
                Source: "NotificationWorker",
                CorrelationId: "corr-invoice-notify-01")
        };
        await store.AppendAsync(sendRecord);
        Console.WriteLine($"✓ [SEND] Notificación '{sendRecord.Resource.Id}' enviada por {sendRecord.Actor.DisplayName}.");

        // AuditAction.Cancel — cancelación de una operación pendiente
        var cancelRecord = new AuditRecord
        {
            Id = AuditId.NewId(),
            OccurredAt = DateTimeOffset.UtcNow,
            Actor = new AuditActor(AuditActorType.User, "usr-alice-404", "alice@domain.com"),
            Action = AuditAction.Cancel,
            Resource = new AuditResource("BankTransfer", "transfer-449922"),
            Outcome = AuditOutcome.Success,
            Context = new AuditContext(
                TenantId: "tenant-fintech",
                Source: "BankingService",
                CausationId: "approve-transfer-cmd-449922"),
            Changes = new[]
            {
                new AuditChange("Status", "Pending", "Cancelled")
            }
        };
        await store.AppendAsync(cancelRecord);
        Console.WriteLine($"✓ [CANCEL] Transferencia '{cancelRecord.Resource.Id}' cancelada. Status: Pending → Cancelled.");

        // AuditAction.Restore — restauración tras borrado suave
        var restoreRecord = new AuditRecord
        {
            Id = AuditId.NewId(),
            OccurredAt = DateTimeOffset.UtcNow,
            Actor = new AuditActor(AuditActorType.User, "usr-bob-102", "bob.admin@domain.com"),
            Action = AuditAction.Restore,
            Resource = new AuditResource("CustomerAccount", "acc-deleted-0041"),
            Outcome = AuditOutcome.Success,
            Context = new AuditContext(
                TenantId: "tenant-fintech",
                Source: "AdminPortal"),
            Changes = new[]
            {
                new AuditChange("IsDeleted", "true", "false"),
                new AuditChange("DeletedAt", "2026-01-15", null)
            }
        };
        await store.AppendAsync(restoreRecord);
        Console.WriteLine($"✓ [RESTORE] Cuenta '{restoreRecord.Resource.Id}' restaurada desde borrado suave.");

        // ── Escenario 5: Evento de plataforma con SystemTenantId ─────────────────
        Console.WriteLine("\n── Escenario 5: Evento de Plataforma (AuditContext.SystemTenantId) ──");
        var platformEvent = new AuditRecord
        {
            Id = AuditId.NewId(),
            OccurredAt = DateTimeOffset.UtcNow,
            Actor = AuditActor.System,
            Action = new AuditAction("CleanupExpiredSessions"),  // Acción custom de sistema
            Resource = new AuditResource("SessionStore", "global"),
            Outcome = AuditOutcome.Success,
            Context = new AuditContext(
                TenantId: AuditContext.SystemTenantId,          // "system" — sin tenant de negocio
                Source: "BackgroundMaintenanceWorker")
        };
        await store.AppendAsync(platformEvent);
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"✓ [SYSTEM] Evento de plataforma con TenantId='{AuditContext.SystemTenantId}' registrado.");
        Console.ResetColor();
        Console.WriteLine("  (No pertenece a ningún tenant de cliente — acción de infraestructura).\n");

        // ── Resumen de Registros para el Tenant ───────────────────────────────────
        var allRecords = await store.QueryAsync(new AuditQuery { TenantId = "tenant-fintech" });
        Console.WriteLine($"✓ Total de eventos de auditoría acumulados para 'tenant-fintech': {allRecords.Records.Count}");
    }
}
