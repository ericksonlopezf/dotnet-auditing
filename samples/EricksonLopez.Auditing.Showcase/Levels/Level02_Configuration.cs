// Copyright © Erickson Lopez. MIT License.
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using EricksonLopez.Auditing.Testing;

namespace EricksonLopez.Auditing.Showcase.Levels;

/// <summary>
/// Nivel 2 — Configuración Completa: Opciones de Runtime, Políticas de Seguridad, Batching y AuditFieldSensitivity
/// </summary>
public static class Level02_Configuration
{
    public static Task RunAsync()
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("\n================================================================================");
        Console.WriteLine(" [NIVEL 2] — CONFIGURACIÓN COMPLETA Y POLÍTICAS DE RUNTIME");
        Console.WriteLine("================================================================================\n");
        Console.ResetColor();

        var services = new ServiceCollection();

        // Configuración integral del pipeline de auditoría
        services.AddAuditing(cfg =>
        {
            // 1. Semántica ante fallas del almacén (FailClosed, FailOpen, Deferred)
            cfg.DefaultFailureBehavior = AuditFailureBehavior.FailClosed;

            // 2. Acciones críticas que SIEMPRE deben ser FailClosed
            cfg.CriticalActionCodes.Add("ProcessWireTransfer");
            cfg.CriticalActionCodes.Add("RevokeAdminPrivileges");

            // 3. Denylist global de campos sensibles (excluidos automáticamente de cambios)
            cfg.GlobalFieldDenylist.Add("CustomerCreditScore");
            cfg.GlobalFieldDenylist.Add("PaymentCardTrack2");

            // 4. Parámetros para procesamiento en lotes (batching)
            cfg.BatchChannelCapacity = 5000;
            cfg.BatchSize = 250;
            cfg.BatchFlushInterval = TimeSpan.FromSeconds(3);

            // 5. Cadena de integridad HMAC — requiere IAuditIntegrityProvider registrado
            // cfg.EnableIntegrityChain = true; // (se activa en Level10)
        })
        .UseStore<InMemoryAuditStore>();

        var serviceProvider = services.BuildServiceProvider();
        var config = serviceProvider.GetRequiredService<AuditConfiguration>();

        Console.WriteLine("✓ Configuración de Auditoría cargada e inyectada:");
        Console.WriteLine($"  • DefaultFailureBehavior: {config.DefaultFailureBehavior}");
        Console.WriteLine($"  • BatchChannelCapacity: {config.BatchChannelCapacity} registros");
        Console.WriteLine($"  • BatchSize: {config.BatchSize} registros por lote");
        Console.WriteLine($"  • BatchFlushInterval: {config.BatchFlushInterval.TotalSeconds}s");
        Console.WriteLine($"  • Acciones Críticas Registradas ({config.CriticalActionCodes.Count}):");
        foreach (var action in config.CriticalActionCodes)
        {
            Console.WriteLine($"    - {action}");
        }

        Console.WriteLine($"\n  • Denylist Global de Campos Sensibles ({config.GlobalFieldDenylist.Count} campos protegidos):");
        int count = 0;
        foreach (var field in config.GlobalFieldDenylist)
        {
            Console.Write($"    {field,-20}");
            if (++count % 3 == 0) Console.WriteLine();
        }
        if (count % 3 != 0) Console.WriteLine();

        // ── AuditFieldSensitivity — política de campo individual ─────────────────
        Console.WriteLine("\n── AuditFieldSensitivity — Políticas de Tratamiento de Campos ──");
        Console.WriteLine("   Define cómo se trata el valor de un campo sensible en AuditChange:");
        Console.WriteLine();
        Console.WriteLine($"   AuditFieldSensitivity.Include  ({(int)AuditFieldSensitivity.Include})  — El campo se registra normalmente (comportamiento por defecto).");
        Console.WriteLine($"   AuditFieldSensitivity.Exclude  ({(int)AuditFieldSensitivity.Exclude})  — El campo se excluye COMPLETAMENTE de los cambios de auditoría.");
        Console.WriteLine($"   AuditFieldSensitivity.Redact   ({(int)AuditFieldSensitivity.Redact})  — El nombre del campo se preserva, el valor se reemplaza con marcador.");
        Console.WriteLine($"   AuditFieldSensitivity.Hash     ({(int)AuditFieldSensitivity.Hash})  — El valor se sustituye por su digest SHA-256 (permite igualdad sin revelar valor).");
        Console.WriteLine();
        Console.WriteLine("   Nota: AuditFieldSensitivity define la POLÍTICA. Su aplicación práctica ocurre");
        Console.WriteLine("   mediante AuditSensitivityPipeline (ver Level10) o al construir cambios explícitos");
        Console.WriteLine("   con AuditChange.Redacted() o AuditSensitivityPipeline.HashValue().\n");

        // ── Demostración práctica de Apply / HashValue ──────────────────────────
        Console.WriteLine("── Demostración de AuditSensitivityPipeline con la configuración anterior ──");
        var sensitivityPipeline = serviceProvider.GetRequiredService<AuditSensitivityPipeline>();

        var rawChanges = new[]
        {
            new AuditChange("EmailAddress", "old@domain.com", "new@domain.com"),        // Incluido normalmente
            new AuditChange("Password", "hash_old_a1b2", "hash_new_c3d4"),              // Denylist → EXCLUIDO
            new AuditChange("CustomerCreditScore", "720", "680"),                       // Denylist custom → EXCLUIDO
            AuditChange.Redacted("SessionToken"),                                       // Redacción explícita → PRESERVADO con IsRedacted=true
            new AuditChange("ProfileHash", null, AuditSensitivityPipeline.HashValue("alice@enterprise.com")) // Hash SHA-256
        };

        var sanitized = sensitivityPipeline.Apply(rawChanges);
        Console.WriteLine($"  Cambios originales:  {rawChanges.Length} campos");
        Console.WriteLine($"  Cambios sanitizados: {sanitized?.Count ?? 0} campos");
        if (sanitized is not null)
        {
            foreach (var ch in sanitized)
            {
                Console.WriteLine($"    Campo: {ch.Field,-20} | Redacted: {ch.IsRedacted} | New: {(ch.NewValue?.Length > 20 ? ch.NewValue[..20] + "..." : ch.NewValue) ?? "null"}");
            }
        }
        Console.WriteLine();

        return Task.CompletedTask;
    }
}
