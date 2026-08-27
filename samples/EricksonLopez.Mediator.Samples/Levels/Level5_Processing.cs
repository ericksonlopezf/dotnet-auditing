// Copyright © Erickson Lopez. MIT License.
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using EricksonLopez.Mediator;

namespace Sample.Levels.Level5_Processing;

// --- 1. Heavy Async Processing Command ---
public sealed record ProcessBatchDataCommand(string BatchId, int ItemCount) : ICommand<int>;

public sealed class ProcessBatchDataCommandHandler : ICommandHandler<ProcessBatchDataCommand, int>
{
    public async ValueTask<int> Handle(ProcessBatchDataCommand command, CancellationToken cancellationToken)
    {
        Console.WriteLine($"[Level 5 - Handler] Starting batch processing '{command.BatchId}' ({command.ItemCount} items)...");
        await Task.Delay(50, cancellationToken).ConfigureAwait(false); // Simulated I/O
        Console.WriteLine($"[Level 5 - Handler] Batch '{command.BatchId}' processed successfully.");
        return command.ItemCount;
    }
}

// --- 2. Notification with Concurrent Parallel Strategy ---
[PublishStrategy(PublishStrategy.Parallel)]
public sealed record RealTimeInventoryUpdatedEvent(string ProductId, int NewStock) : INotification;

public sealed class InventoryIndexNotificationHandler : INotificationHandler<RealTimeInventoryUpdatedEvent>
{
    public async ValueTask Handle(RealTimeInventoryUpdatedEvent notification, CancellationToken cancellationToken)
    {
        Console.WriteLine($"[Level 5 - Parallel Handler A] Updating search index for {notification.ProductId}...");
        await Task.Delay(40, cancellationToken).ConfigureAwait(false);
        Console.WriteLine($"[Level 5 - Parallel Handler A] Index updated to stock={notification.NewStock}.");
    }
}

public sealed class InventoryCacheNotificationHandler : INotificationHandler<RealTimeInventoryUpdatedEvent>
{
    public async ValueTask Handle(RealTimeInventoryUpdatedEvent notification, CancellationToken cancellationToken)
    {
        Console.WriteLine($"[Level 5 - Parallel Handler B] Invalidating distributed cache for {notification.ProductId}...");
        await Task.Delay(40, cancellationToken).ConfigureAwait(false);
        Console.WriteLine($"[Level 5 - Parallel Handler B] Cache invalidated.");
    }
}

public sealed class InventoryDashboardNotificationHandler : INotificationHandler<RealTimeInventoryUpdatedEvent>
{
    public async ValueTask Handle(RealTimeInventoryUpdatedEvent notification, CancellationToken cancellationToken)
    {
        Console.WriteLine($"[Level 5 - Parallel Handler C] Notifying real-time monitoring dashboard...");
        await Task.Delay(40, cancellationToken).ConfigureAwait(false);
        Console.WriteLine($"[Level 5 - Parallel Handler C] Dashboard updated.");
    }
}

/// <summary>
/// Level 5: Concurrent Processing and Publishing Strategies.
/// </summary>
public static class Demo
{
    public static async Task RunAsync(IMediator mediator)
    {
        Console.WriteLine("================================================================================");
        Console.WriteLine("  LEVEL 5: ASYNCHRONOUS PROCESSING & CONCURRENT STRATEGIES");
        Console.WriteLine("================================================================================");

        // 1. Asynchronous Command
        Console.WriteLine("1. Non-blocking Asynchronous Processing:");
        var batchCmd = new ProcessBatchDataCommand("BATCH-2026-X", 500);
        var processedCount = await mediator.Send(batchCmd, CancellationToken.None);
        Console.WriteLine($"   -> Total items processed: {processedCount}");
        Console.WriteLine();

        // 2. Parallel Concurrent Publishing with Task.WhenAll
        Console.WriteLine("2. Parallel Publishing with [PublishStrategy(PublishStrategy.Parallel)]:");
        var sw = Stopwatch.StartNew();
        var parallelEvent = new RealTimeInventoryUpdatedEvent("PRD-SERVER-99", 42);
        await mediator.Publish(parallelEvent, CancellationToken.None);
        sw.Stop();
        Console.WriteLine($"   -> All concurrent handlers completed in {sw.ElapsedMilliseconds} ms (parallel execution).");

        Console.WriteLine("--------------------------------------------------------------------------------\n");
    }
}
