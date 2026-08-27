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
            Console.WriteLine(" Select a pedagogical level to execute:");
            Console.WriteLine(" ─────────────────────────────────────────────────────────────────────────────");
            Console.WriteLine(" [0]  Level 0  — Conceptual (Fundamentals, Philosophy & Architectural Boundaries)");
            Console.WriteLine(" [1]  Level 1  — Quick Start (Minimal Setup, DI & First Audit Record)");
            Console.WriteLine(" [2]  Level 2  — Full Configuration (Runtime Policies & Batching)");
            Console.WriteLine(" [3]  Level 3  — Real-World Use Cases (Business Workflows & Permissions)");
            Console.WriteLine(" [4]  Level 4  — Advanced Integration (Ambient AuditScope & Hierarchies)");
            Console.WriteLine(" [5]  Level 5  — Batch Processing (High-Throughput Batching)");
            Console.WriteLine(" [6]  Level 6  — Error Handling (Fail Semantics & Outcomes)");
            Console.WriteLine(" [7]  Level 7  — Scalability (Keyset Cursor Pagination)");
            Console.WriteLine(" [8]  Level 8  — Customization (Custom Providers for Actor, Key & Store)");
            Console.WriteLine(" [9]  Level 9  — Persistence Providers & OpenTelemetry)");
            Console.WriteLine(" [10] Level 10 — Enterprise Architecture (HMAC Tamper Detection & GDPR)");
            Console.WriteLine(" [A]  Run ALL levels sequentially");
            Console.WriteLine(" [Q]  Quit");
            Console.WriteLine(" ─────────────────────────────────────────────────────────────────────────────");
            Console.Write(" Enter an option: ");

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
                        Console.WriteLine("Invalid option. Please try again.");
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"\n[ERROR] Error during level execution: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                Console.ResetColor();
            }

            Console.WriteLine("\nPress ENTER to continue...");
            Console.ReadLine();
        }
    }

    private static async Task RunAllLevelsAsync()
    {
        var totalSw = Stopwatch.StartNew();

        PrintHeader();
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine(" >> RUNNING FULL SHOWCASE SUITE (LEVELS 0 THROUGH 10) <<\n");
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
        Console.WriteLine($" ✓ SHOWCASE SUITE COMPLETED SUCCESSFULLY in {totalSw.Elapsed.TotalMilliseconds:F2} ms");
        Console.WriteLine("================================================================================\n");
        Console.ResetColor();
    }

    private static void PrintHeader()
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine(@"
╔══════════════════════════════════════════════════════════════════════════════╗
║                   ERICKSONLOPEZ.AUDITING — OFFICIAL SHOWCASE                 ║
║            Executable Reference Implementation & API Documentation           ║
╚══════════════════════════════════════════════════════════════════════════════╝");
        Console.ResetColor();
    }
}
