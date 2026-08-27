// Copyright © Erickson Lopez. MIT License.
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EricksonLopez.Mediator;
using Microsoft.Extensions.DependencyInjection;

namespace EricksonLopez.Mediator.AotTest;

// ─── 1. Commands & Handlers ───────────────────────────────────────────────────

public record PingCommand(string Message) : ICommand<string>;

[ServiceLifetime(HandlerLifetime.Singleton)]
public class PingCommandHandler : ICommandHandler<PingCommand, string>
{
    public ValueTask<string> Handle(PingCommand command, CancellationToken cancellationToken)
    {
        return new ValueTask<string>($"Pong: {command.Message}");
    }
}

// ─── 2. Pipeline Behaviors ────────────────────────────────────────────────────

[UseBehavior(typeof(CommandLoggingBehavior), 1)]
[UseBehavior(typeof(CommandTimingBehavior), 2)]
public record PingWithBehaviorsCommand(string Value) : ICommand<string>;

public class CommandLoggingBehavior : IPipelineBehavior<PingWithBehaviorsCommand, string>
{
    public static int ExecutionCount = 0;
    public async ValueTask<string> Handle<TNext>(PingWithBehaviorsCommand request, TNext next, CancellationToken cancellationToken)
        where TNext : struct, INext<string>
    {
        ExecutionCount++;
        return await next.InvokeAsync().ConfigureAwait(false);
    }
}

public class CommandTimingBehavior : IPipelineBehavior<PingWithBehaviorsCommand, string>
{
    public static int ExecutionCount = 0;
    public async ValueTask<string> Handle<TNext>(PingWithBehaviorsCommand request, TNext next, CancellationToken cancellationToken)
        where TNext : struct, INext<string>
    {
        ExecutionCount++;
        return await next.InvokeAsync().ConfigureAwait(false);
    }
}

public class PingWithBehaviorsCommandHandler : ICommandHandler<PingWithBehaviorsCommand, string>
{
    public ValueTask<string> Handle(PingWithBehaviorsCommand command, CancellationToken cancellationToken)
    {
        return new ValueTask<string>($"Result: {command.Value}");
    }
}

// ─── 3. Queries & Lifetimes ───────────────────────────────────────────────────

public record GetUserQuery(int Id) : IQuery<string>;

[ServiceLifetime(HandlerLifetime.Scoped)]
public class GetUserQueryHandler : IQueryHandler<GetUserQuery, string>
{
    public ValueTask<string> Handle(GetUserQuery query, CancellationToken cancellationToken)
    {
        return new ValueTask<string>($"User_{query.Id}");
    }
}

// ─── 4. Notifications (Sequential) ───────────────────────────────────────────

[PublishStrategy(PublishStrategy.Sequential)]
public record OrderPlacedNotification(int OrderId) : INotification;

public class OrderNotificationHandler1 : INotificationHandler<OrderPlacedNotification>
{
    public static int HandledCount = 0;
    public ValueTask Handle(OrderPlacedNotification notification, CancellationToken cancellationToken)
    {
        HandledCount++;
        return default;
    }
}

public class OrderNotificationHandler2 : INotificationHandler<OrderPlacedNotification>
{
    public static int HandledCount = 0;
    public ValueTask Handle(OrderPlacedNotification notification, CancellationToken cancellationToken)
    {
        HandledCount++;
        return default;
    }
}

// ─── 5. Notifications (Parallel) ─────────────────────────────────────────────

[PublishStrategy(PublishStrategy.Parallel)]
public record InventoryUpdatedNotification(string Sku) : INotification;

public class InventoryNotificationHandler1 : INotificationHandler<InventoryUpdatedNotification>
{
    public static int HandledCount = 0;
    public ValueTask Handle(InventoryUpdatedNotification notification, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref HandledCount);
        return default;
    }
}

public class InventoryNotificationHandler2 : INotificationHandler<InventoryUpdatedNotification>
{
    public static int HandledCount = 0;
    public ValueTask Handle(InventoryUpdatedNotification notification, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref HandledCount);
        return default;
    }
}

// ─── 6. Notification Behaviors ────────────────────────────────────────────────

[UseBehavior(typeof(AuditNotificationBehavior))]
public record CustomerCreatedNotification(string Email) : INotification;

public class AuditNotificationBehavior : INotificationBehavior<CustomerCreatedNotification>
{
    public static int BehaviorExecuted = 0;
    public async ValueTask Handle<TNext>(CustomerCreatedNotification notification, TNext next, CancellationToken cancellationToken)
        where TNext : struct, INext
    {
        BehaviorExecuted++;
        await next.InvokeAsync().ConfigureAwait(false);
    }
}

public class CustomerCreatedNotificationHandler : INotificationHandler<CustomerCreatedNotification>
{
    public static int HandlerExecuted = 0;
    public ValueTask Handle(CustomerCreatedNotification notification, CancellationToken cancellationToken)
    {
        HandlerExecuted++;
        return default;
    }
}

// ─── 7. Nested Send ──────────────────────────────────────────────────────────

public record OuterCommand(string Payload) : ICommand<string>;

public class OuterCommandHandler : ICommandHandler<OuterCommand, string>
{
    private readonly ISender _sender;
    public OuterCommandHandler(ISender sender) => _sender = sender;

    public async ValueTask<string> Handle(OuterCommand command, CancellationToken cancellationToken)
    {
        var innerResult = await _sender.Send(new PingCommand(command.Payload), cancellationToken).ConfigureAwait(false);
        return $"Outer[{innerResult}]";
    }
}

// ─── Program Execution ───────────────────────────────────────────────────────

class Program
{
    static async Task<int> Main(string[] args)
    {
        Console.WriteLine("=== Starting EricksonLopez.Mediator Comprehensive Native AOT Test ===");

        var services = new ServiceCollection();
        services.AddEricksonLopezMediator();
        var provider = services.BuildServiceProvider();

        using var scope = provider.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var publisher = scope.ServiceProvider.GetRequiredService<IPublisher>();

        // Test 1: Simple Command Dispatch
        var r1 = await mediator.Send(new PingCommand("Hello"));
        if (r1 != "Pong: Hello") { Console.WriteLine($"FAIL: r1 was {r1}"); return 1; }
        Console.WriteLine("[PASS] 1. Simple Command Dispatch");

        // Test 2: Command with 2 Pipeline Behaviors
        var r2 = await mediator.Send(new PingWithBehaviorsCommand("Fast"));
        if (r2 != "Result: Fast" || CommandLoggingBehavior.ExecutionCount != 1 || CommandTimingBehavior.ExecutionCount != 1)
        {
            Console.WriteLine($"FAIL: r2 was {r2}, LoggingCount={CommandLoggingBehavior.ExecutionCount}, TimingCount={CommandTimingBehavior.ExecutionCount}");
            return 1;
        }
        Console.WriteLine("[PASS] 2. Command with Pipeline Behaviors");

        // Test 3: Query Handler with Scoped Lifetime
        var r3 = await sender.Send(new GetUserQuery(42));
        if (r3 != "User_42") { Console.WriteLine($"FAIL: r3 was {r3}"); return 1; }
        Console.WriteLine("[PASS] 3. Query Handler (Scoped)");

        // Test 4: Sequential Notifications
        await publisher.Publish(new OrderPlacedNotification(101));
        if (OrderNotificationHandler1.HandledCount != 1 || OrderNotificationHandler2.HandledCount != 1)
        {
            Console.WriteLine("FAIL: Sequential Notification handlers not called properly.");
            return 1;
        }
        Console.WriteLine("[PASS] 4. Sequential Notifications");

        // Test 5: Parallel Notifications
        await publisher.Publish(new InventoryUpdatedNotification("SKU-999"));
        if (InventoryNotificationHandler1.HandledCount != 1 || InventoryNotificationHandler2.HandledCount != 1)
        {
            Console.WriteLine("FAIL: Parallel Notification handlers not called properly.");
            return 1;
        }
        Console.WriteLine("[PASS] 5. Parallel Notifications");

        // Test 6: Notification Behavior
        await publisher.Publish(new CustomerCreatedNotification("user@example.com"));
        if (AuditNotificationBehavior.BehaviorExecuted != 1 || CustomerCreatedNotificationHandler.HandlerExecuted != 1)
        {
            Console.WriteLine("FAIL: Notification Behavior not executed.");
            return 1;
        }
        Console.WriteLine("[PASS] 6. Notification Behaviors");

        // Test 7: Nested Send via ISender
        var r7 = await mediator.Send(new OuterCommand("NestedTest"));
        if (r7 != "Outer[Pong: NestedTest]") { Console.WriteLine($"FAIL: r7 was {r7}"); return 1; }
        Console.WriteLine("[PASS] 7. Nested Send via ISender");

        Console.WriteLine("\n✅ ALL NATIVE AOT SCENARIOS VERIFIED SUCCESSFULLY!");
        return 0;
    }
}



