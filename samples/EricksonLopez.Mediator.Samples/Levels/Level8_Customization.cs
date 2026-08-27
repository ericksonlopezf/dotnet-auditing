// Copyright © Erickson Lopez. MIT License.
using System;
using System.Threading;
using System.Threading.Tasks;
using EricksonLopez.Mediator;

// Instruct Roslyn Source Generator to scan additional assemblies at compile-time
[assembly: DiscoverHandlers(typeof(Sample.Levels.Level8_Customization.MarkerAssemblyType))]

namespace Sample.Levels.Level8_Customization;

/// <summary>
/// Marker type utilized by <see cref="DiscoverHandlersAttribute"/>.
/// </summary>
public static class MarkerAssemblyType { }

// --- 1. Handler with Singleton Lifetime ---
public sealed record IncrementCounterCommand() : ICommand<int>;

[ServiceLifetime(HandlerLifetime.Singleton)]
public sealed class StatefulCounterCommandHandler : ICommandHandler<IncrementCounterCommand, int>
{
    private int _counter = 0;

    public ValueTask<int> Handle(IncrementCounterCommand command, CancellationToken cancellationToken)
    {
        _counter++;
        Console.WriteLine($"[Level 8 - Singleton Handler] Internal state counter: {_counter}");
        return ValueTask.FromResult(_counter);
    }
}

// --- 2. Handler with Transient Lifetime (Default) ---
public sealed record TransientWorkerCommand() : ICommand<Guid>;

[ServiceLifetime(HandlerLifetime.Transient)]
public sealed class TransientWorkerCommandHandler : ICommandHandler<TransientWorkerCommand, Guid>
{
    private readonly Guid _instanceId = Guid.NewGuid();

    public ValueTask<Guid> Handle(TransientWorkerCommand command, CancellationToken cancellationToken)
    {
        Console.WriteLine($"[Level 8 - Transient Handler] Unique instance created: {_instanceId}");
        return ValueTask.FromResult(_instanceId);
    }
}

/// <summary>
/// Level 8: Service Lifetime Customization (ServiceLifetime) and Assembly Discovery.
/// </summary>
public static class Demo
{
    public static async Task RunAsync(IMediator mediator)
    {
        Console.WriteLine("================================================================================");
        Console.WriteLine("  LEVEL 8: CUSTOMIZATION (SERVICE LIFETIMES & DISCOVER HANDLERS)");
        Console.WriteLine("================================================================================");

        // 1. Singleton Handler (Maintains accumulated state)
        Console.WriteLine("1. Handler with [ServiceLifetime(HandlerLifetime.Singleton)]:");
        var c1 = await mediator.Send(new IncrementCounterCommand(), CancellationToken.None);
        var c2 = await mediator.Send(new IncrementCounterCommand(), CancellationToken.None);
        Console.WriteLine($"   -> Final accumulated value in same Singleton instance: {c2}");
        Console.WriteLine();

        // 2. Transient Handler (New instance on each invocation)
        Console.WriteLine("2. Handler with [ServiceLifetime(HandlerLifetime.Transient)]:");
        var id1 = await mediator.Send(new TransientWorkerCommand(), CancellationToken.None);
        var id2 = await mediator.Send(new TransientWorkerCommand(), CancellationToken.None);
        Console.WriteLine($"   -> Instance 1 != Instance 2 ({id1 != id2})");
        Console.WriteLine();

        // 3. Scoped Handler (One instance per DI scope — typical in web requests with DbContext)
        Console.WriteLine("3. Handler with [ServiceLifetime(HandlerLifetime.Scoped)] — per-request DI scope:");
        Console.WriteLine("   [ServiceLifetime(HandlerLifetime.Scoped)] on ScopedDbWorkCommandHandler");
        Console.WriteLine("   -> Use when the handler depends on scoped services (e.g., EF Core DbContext).");
        Console.WriteLine("   -> The DI container creates one instance per IServiceScope.CreateScope().");
        using var scope = ((IServiceProvider)null!).CreateScopedMediatorScope();
        Console.WriteLine("   -> Scoped lifetime ensures safe usage in background services via IServiceScopeFactory.");
        Console.WriteLine();

        // 4. [DiscoverHandlers] Attribute
        Console.WriteLine("4. Multi-Assembly Discovery without runtime reflection:");
        Console.WriteLine("   [assembly: DiscoverHandlers(typeof(MarkerAssemblyType))]");
        Console.WriteLine("   Enables Roslyn Source Generator to emit unified dispatching for external types.");

        Console.WriteLine("--------------------------------------------------------------------------------\n");
    }
}

/// <summary>
/// Extension helper demonstrating the idiomatic pattern for scoped handlers.
/// </summary>
internal static class ScopedMediatorScopeExtensions
{
    /// <summary>
    /// Creates a disposable representation of a DI scope for documentation purposes.
    /// In production, use <c>IServiceScopeFactory.CreateScope()</c>.
    /// </summary>
    internal static ScopedLifetimeScope CreateScopedMediatorScope(this IServiceProvider? provider)
        => new ScopedLifetimeScope();
}

/// <summary>
/// Represents a disposable DI scope used by scoped handlers.
/// This type illustrates the pattern — production code uses <c>IServiceScopeFactory</c>.
/// </summary>
public sealed class ScopedLifetimeScope : IDisposable
{
    public void Dispose() { /* scope teardown */ }
}

// --- 3. Handler with Scoped Lifetime ---
public sealed record ScopedDbWorkCommand(string WorkItemId) : ICommand<bool>;

/// <summary>
/// Demonstrates <see cref="HandlerLifetime.Scoped"/> — the DI container creates one instance
/// per <c>IServiceScope</c>. This is the correct lifetime for handlers that depend on
/// EF Core <c>DbContext</c>, <c>IUnitOfWork</c>, or any other scoped service.
/// </summary>
[ServiceLifetime(HandlerLifetime.Scoped)]
public sealed class ScopedDbWorkCommandHandler : ICommandHandler<ScopedDbWorkCommand, bool>
{
    private readonly Guid _scopeId = Guid.NewGuid();

    public ValueTask<bool> Handle(ScopedDbWorkCommand command, CancellationToken cancellationToken)
    {
        Console.WriteLine($"[Level 8 - Scoped Handler] Scope instance: {_scopeId} | WorkItem: {command.WorkItemId}");
        return ValueTask.FromResult(true);
    }
}
