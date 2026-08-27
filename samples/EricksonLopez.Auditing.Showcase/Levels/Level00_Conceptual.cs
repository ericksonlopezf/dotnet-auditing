// Copyright © Erickson Lopez. MIT License.
using System;

namespace EricksonLopez.Auditing.Showcase.Levels;

/// <summary>
/// Level 0 — Conceptual: Core Principles, Philosophy & Canonical Domain Model
/// </summary>
public static class Level00_Conceptual
{
    public static void Run()
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("\n================================================================================");
        Console.WriteLine(" [LEVEL 0] — CONCEPTUAL FUNDAMENTALS OF ERICKSONLOPEZ.AUDITING");
        Console.WriteLine("================================================================================\n");
        Console.ResetColor();

        Console.WriteLine("1. What is EricksonLopez.Auditing?");
        Console.WriteLine("   A high-performance forensic and compliance audit library designed for");
        Console.WriteLine("   .NET 8, .NET 9, and .NET 10, Native AOT-first, multi-tenant by design,");
        Console.WriteLine("   featuring an immutable canonical model and HMAC-SHA256 tamper evidence.\n");

        Console.WriteLine("2. Fundamental Invariant Answered:");
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("   » WHO (Actor) · DID WHAT (Action) · ON WHAT (Resource) · WHEN (OccurredAt)");
        Console.WriteLine("     · FROM WHERE (Context) · WITH WHAT RESULT (Outcome) «\n");
        Console.ResetColor();

        Console.WriteLine("3. What this library is NOT (Architectural Boundaries):");
        Console.WriteLine("   ┌─────────────────────────────┬─────────────────────────────────────────────────┐");
        Console.WriteLine("   │ ❌ NOT THIS                 │ ✅ THIS                                         │");
        Console.WriteLine("   ├─────────────────────────────┼─────────────────────────────────────────────────┤");
        Console.WriteLine("   │ ILogger / Logging framework │ Structured, immutable audit trail               │");
        Console.WriteLine("   │ OpenTelemetry / Tracing APM │ Legal compliance & regulatory evidence          │");
        Console.WriteLine("   │ Event Sourcing              │ Immutable record of observed historical facts   │");
        Console.WriteLine("   │ Security / Identity Provider│ Consumer of authenticated identity contexts     │");
        Console.WriteLine("   │ EF Core Change Tracker      │ Decoupled Actor/Resource/Context domain model   │");
        Console.WriteLine("   │ Outbox / Message Broker     │ Guaranteed append-only storage persistence      │");
        Console.WriteLine("   └─────────────────────────────┴─────────────────────────────────────────────────┘\n");

        Console.WriteLine("4. Security & Design Invariants:");
        Console.WriteLine("   • Append-Only: No UpdateAsync or DeleteAsync methods exist on IAuditStore.");
        Console.WriteLine("   • Mandatory Tenant: Every audit record belongs to an explicit TenantId.");
        Console.WriteLine("   • Monotonic Ordering: Identifiers generated via RFC 9562 UUIDv7.");
        Console.WriteLine("   • Sensitive Data Protection: Global denylist masks passwords/tokens by default.");
        Console.WriteLine("   • Cryptographic Integrity: Tamper-evident HMAC-SHA256 hash chaining.");
        Console.WriteLine("   • Zero Reflection at Runtime: 100% Native AOT & IL Trimming compatible.\n");
    }
}
