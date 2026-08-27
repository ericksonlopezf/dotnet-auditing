// Copyright © Erickson Lopez. MIT License.
using System;
using System.Threading.Tasks;

namespace Sample.Levels.Level0_Conceptual;

/// <summary>
/// Level 0: Conceptual Foundation of EricksonLopez.Mediator.
/// </summary>
public static class Demo
{
    /// <summary>
    /// Executes the conceptual overview presentation.
    /// </summary>
    public static Task RunAsync()
    {
        Console.WriteLine("================================================================================");
        Console.WriteLine("  LEVEL 0: CONCEPTUAL FOUNDATIONS OF ERICKSONLOPEZ.MEDIATOR");
        Console.WriteLine("================================================================================");
        Console.WriteLine("1. What is EricksonLopez.Mediator?");
        Console.WriteLine("   It is an AOT-first, compile-time generated Application Dispatcher designed for");
        Console.WriteLine("   CQRS patterns, command/query mediation, and notification publishing with zero");
        Console.WriteLine("   runtime reflection overhead.");
        Console.WriteLine();
        Console.WriteLine("2. What problem does it solve?");
        Console.WriteLine("   It eliminates the performance bottlenecks of traditional mediator libraries:");
        Console.WriteLine("   - Zero dynamic runtime assembly scanning.");
        Console.WriteLine("   - No 'MakeGenericType' or reflection-based invocations.");
        Console.WriteLine("   - Eliminates pipeline heap allocations by replacing delegates with struct INext<T>.");
        Console.WriteLine("   - 100% compatible with Native AOT and Trimming in .NET 8, .NET 9, and .NET 10.");
        Console.WriteLine();
        Console.WriteLine("3. Architectural Comparison Table:");
        Console.WriteLine("   ┌───────────────────────────┬──────────────────────┬─────────────────────────┐");
        Console.WriteLine("   │ Feature                   │ EricksonLopez.Mediator│ Traditional MediatR     │");
        Console.WriteLine("   ├───────────────────────────┼──────────────────────┼─────────────────────────┤");
        Console.WriteLine("   │ Dispatch Generation       │ Roslyn Source Gen    │ Dynamic Reflection      │");
        Console.WriteLine("   │ Native AOT Compatibility  │ 100% Native (0 warn) │ Limited / Warnings      │");
        Console.WriteLine("   │ Pipeline Allocations      │ 0 B (Struct INext<T>)│ Heap (RequestHandlerDel)│");
        Console.WriteLine("   │ CQRS Typing               │ ICommand vs IQuery   │ Unified IRequest        │");
        Console.WriteLine("   │ Canonical Return Type     │ ValueTask<T>         │ Task<T>                 │");
        Console.WriteLine("   │ Static Dispatch (Zero-DI) │ Supported (Static)   │ Not Supported           │");
        Console.WriteLine("   │ Reactive Streaming        │ IStreamRequest<T>    │ IStreamRequest<T>       │");
        Console.WriteLine("   └───────────────────────────┴──────────────────────┴─────────────────────────┘");
        Console.WriteLine();
        Console.WriteLine("4. Design Philosophy:");
        Console.WriteLine("   - Compile-Time Safety: Missing handlers are detected at compile-time via Roslyn.");
        Console.WriteLine("   - Strict Segregation: ICommand (mutations) != IQuery (pure reads).");
        Console.WriteLine("   - Zero-Allocation: Ultra-fast pipelines built for high concurrency.");
        Console.WriteLine("--------------------------------------------------------------------------------\n");
        return Task.CompletedTask;
    }
}
