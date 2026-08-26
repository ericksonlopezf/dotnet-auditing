// Copyright © Erickson Lopez. MIT License.
using System;

namespace EricksonLopez.Auditing.Showcase.Levels;

/// <summary>
/// Nivel 0 — Conceptual: Principios Fundamentales, Filosofía y Modelo Canónico
/// </summary>
public static class Level00_Conceptual
{
    public static void Run()
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("\n================================================================================");
        Console.WriteLine(" [NIVEL 0] — FUNDAMENTOS CONCEPTUALES DE ERICKSONLOPEZ.AUDITING");
        Console.WriteLine("================================================================================\n");
        Console.ResetColor();

        Console.WriteLine("1. ¿Qué es EricksonLopez.Auditing?");
        Console.WriteLine("   Es una librería de auditoría forense y de cumplimiento normativo (compliance)");
        Console.WriteLine("   diseñada para .NET 8, .NET 9 y .NET 10, Native AOT-first, multi-tenant por");
        Console.WriteLine("   diseño, con modelo canónico inmutable y soporte criptográfico HMAC-SHA256.\n");

        Console.WriteLine("2. Pregunta Fundamental que Responde:");
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("   » QUIÉN (Actor) · HIZO QUÉ (Action) · SOBRE QUÉ (Resource) · CUÁNDO (OccurredAt)");
        Console.WriteLine("     · DESDE DÓNDE (Context) · CON QUÉ RESULTADO (Outcome) «\n");
        Console.ResetColor();

        Console.WriteLine("3. ¿Qué NO es la librería? (Límites Arquitectónicos Claros):");
        Console.WriteLine("   ┌─────────────────────────────┬─────────────────────────────────────────────────┐");
        Console.WriteLine("   │ ❌ NO ES                    │ ✅ ES                                           │");
        Console.WriteLine("   ├─────────────────────────────┼─────────────────────────────────────────────────┤");
        Console.WriteLine("   │ ILogger / Logging framework │ Audit Trail estructurado e inmutable            │");
        Console.WriteLine("   │ OpenTelemetry / Tracing APM │ Evidencia legal / auditoría de cumplimiento     │");
        Console.WriteLine("   │ Event Sourcing              │ Registro inmutable de hechos observados         │");
        Console.WriteLine("   │ Security / Identity Provider│ Consumidor de identidades autenticadas          │");
        Console.WriteLine("   │ EF Core Change Tracker      │ Modelo de Actor/Resource/Context independiente  │");
        Console.WriteLine("   │ Outbox / Message Broker     │ Almacenamiento append-only garantizado          │");
        Console.WriteLine("   └─────────────────────────────┴─────────────────────────────────────────────────┘\n");

        Console.WriteLine("4. Invariantes de Seguridad y Diseño:");
        Console.WriteLine("   • Append-Only: No existen métodos UpdateAsync ni DeleteAsync en IAuditStore.");
        Console.WriteLine("   • Tenant Obligatorio: Todo registro pertenece a un TenantId explícito.");
        Console.WriteLine("   • Orden Cronológico: Identificadores basados en UUIDv7 (RFC 9562).");
        Console.WriteLine("   • Sensitive Data Protection: Denylist global de contraseñas y tokens por defecto.");
        Console.WriteLine("   • Integridad Criptográfica: Enlace de hash HMAC-SHA256 resistente a manipulación.");
        Console.WriteLine("   • Zero Reflection en runtime: 100% compatible con Native AOT / Trimming.\n");
    }
}
