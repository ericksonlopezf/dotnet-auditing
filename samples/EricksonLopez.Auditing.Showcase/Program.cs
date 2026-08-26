// Copyright © Erickson Lopez. MIT License.
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using EricksonLopez.Auditing.Showcase.Levels;

namespace EricksonLopez.Auditing.Showcase;

public static class Program
{
    public static async Task Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        bool runAll = args.Length > 0 && (args[0] == "--all" || args[0] == "-a");

        if (runAll)
        {
            await RunAllLevelsAsync();
            return;
        }

        while (true)
        {
            PrintHeader();
            Console.WriteLine(" Seleccione el nivel pedagógico a ejecutar:");
            Console.WriteLine(" ─────────────────────────────────────────────────────────────────────────────");
            Console.WriteLine(" [0]  Nivel 0  — Conceptual (Fundamentos, Filosofía y Límites Arquitectónicos)");
            Console.WriteLine(" [1]  Nivel 1  — Inicio Rápido (Configuración Mínima, DI y Primer Registro)");
            Console.WriteLine(" [2]  Nivel 2  — Configuración Completa (Políticas de Runtime y Batching)");
            Console.WriteLine(" [3]  Nivel 3  — Casos de Uso Reales (Flujos de Negocio y Permisos)");
            Console.WriteLine(" [4]  Nivel 4  — Integración Avanzada (AuditScope Ambiental y Jerarquías)");
            Console.WriteLine(" [5]  Nivel 5  — Procesamiento en Lotes (High-Throughput Batching)");
            Console.WriteLine(" [6]  Nivel 6  — Manejo de Errores (Fail Semantics y Outcomes)");
            Console.WriteLine(" [7]  Nivel 7  — Escalabilidad (Keyset Cursor Pagination)");
            Console.WriteLine(" [8]  Nivel 8  — Personalización (Proveedores Custom de Actor, Clave y Store)");
            Console.WriteLine(" [9]  Nivel 9  — Adaptadores de Persistencia y OpenTelemetry)");
            Console.WriteLine(" [10] Nivel 10 — Arquitectura Empresarial (HMAC Tamper Detection y GDPR)");
            Console.WriteLine(" [A]  Ejecutar TODOS los niveles secuencialmente");
            Console.WriteLine(" [Q]  Salir");
            Console.WriteLine(" ─────────────────────────────────────────────────────────────────────────────");
            Console.Write(" Ingrese una opción: ");

            var input = Console.ReadLine()?.Trim().ToUpperInvariant();

            if (input == "Q")
            {
                break;
            }

            try
            {
                switch (input)
                {
                    case "0":
                        Level00_Conceptual.Run();
                        break;
                    case "1":
                        await Level01_QuickStart.RunAsync();
                        break;
                    case "2":
                        await Level02_Configuration.RunAsync();
                        break;
                    case "3":
                        await Level03_RealWorldUseCases.RunAsync();
                        break;
                    case "4":
                        await Level04_AdvancedIntegration.RunAsync();
                        break;
                    case "5":
                        await Level05_BatchProcessing.RunAsync();
                        break;
                    case "6":
                        await Level06_ErrorHandling.RunAsync();
                        break;
                    case "7":
                        await Level07_Scalability.RunAsync();
                        break;
                    case "8":
                        await Level08_Customization.RunAsync();
                        break;
                    case "9":
                        await Level09_Providers.RunAsync();
                        break;
                    case "10":
                        await Level10_EnterpriseArchitecture.RunAsync();
                        break;
                    case "A":
                        await RunAllLevelsAsync();
                        break;
                    default:
                        Console.WriteLine("Opción no válida. Intente nuevamente.");
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"\n[ERROR] Error durante la ejecución del nivel: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                Console.ResetColor();
            }

            Console.WriteLine("\nPresione ENTER para continuar...");
            Console.ReadLine();
        }
    }

    private static async Task RunAllLevelsAsync()
    {
        var totalSw = Stopwatch.StartNew();

        PrintHeader();
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine(" >> EJECUTANDO SUITE COMPLETA DEL SHOWCASE (NIVELES 0 AL 10) <<\n");
        Console.ResetColor();

        Level00_Conceptual.Run();
        await Level01_QuickStart.RunAsync();
        await Level02_Configuration.RunAsync();
        await Level03_RealWorldUseCases.RunAsync();
        await Level04_AdvancedIntegration.RunAsync();
        await Level05_BatchProcessing.RunAsync();
        await Level06_ErrorHandling.RunAsync();
        await Level07_Scalability.RunAsync();
        await Level08_Customization.RunAsync();
        await Level09_Providers.RunAsync();
        await Level10_EnterpriseArchitecture.RunAsync();

        totalSw.Stop();
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("================================================================================");
        Console.WriteLine($" ✓ SUITE DE SHOWCASE COMPLETADA EXITOSAMENTE en {totalSw.Elapsed.TotalMilliseconds:F2} ms");
        Console.WriteLine("================================================================================\n");
        Console.ResetColor();
    }

    private static void PrintHeader()
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine(@"
╔══════════════════════════════════════════════════════════════════════════════╗
║                   ERICKSONLOPEZ.AUDITING — SHOWCASE OFICIAL                  ║
║      Implementación de Referencia Ejecutable y Documentación de la API       ║
╚══════════════════════════════════════════════════════════════════════════════╝");
        Console.ResetColor();
    }
}
