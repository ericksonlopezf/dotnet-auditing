// Copyright © Erickson Lopez. MIT License.
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using EricksonLopez.Mediator;
using EricksonLopez.Mediator.Testing;

namespace Sample.Levels.Level11_Testing;

// ─────────────────────────────────────────────────────────────────────────────
// Domain contracts used exclusively in this testing showcase
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>Command to place a customer order.</summary>
public sealed record PlaceOrderCommand(string ProductId, int Quantity) : ICommand<OrderReceipt>;

/// <summary>DTO returned by the order handler.</summary>
public sealed record OrderReceipt(Guid OrderId, string Status);

/// <summary>Query to retrieve a product price.</summary>
public sealed record GetProductPriceQuery(string ProductId) : IQuery<decimal>;

/// <summary>Notification raised when an order is confirmed.</summary>
public sealed record OrderConfirmedEvent(Guid OrderId) : INotification;

/// <summary>Streaming request that emits price history entries.</summary>
public sealed record PriceHistoryStreamRequest(string ProductId) : IStreamRequest<decimal>;

// ─────────────────────────────────────────────────────────────────────────────
// Stub handlers — required by the Roslyn Source Generator (ELM001/006/009).
// At runtime these handlers are NOT invoked because FakeMediator is used
// for all dispatch in this level. They exist only to pass compile-time
// handler-discovery checks enforced by the Generator.
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>Stub handler satisfying the compile-time generator requirement for <see cref="PlaceOrderCommand"/>.</summary>
internal sealed class PlaceOrderCommandHandler : ICommandHandler<PlaceOrderCommand, OrderReceipt>
{
    public ValueTask<OrderReceipt> Handle(PlaceOrderCommand command, CancellationToken cancellationToken)
        => ValueTask.FromResult(new OrderReceipt(Guid.NewGuid(), "STUB"));
}

/// <summary>Stub handler satisfying the compile-time generator requirement for <see cref="GetProductPriceQuery"/>.</summary>
internal sealed class GetProductPriceQueryHandler : IQueryHandler<GetProductPriceQuery, decimal>
{
    public ValueTask<decimal> Handle(GetProductPriceQuery query, CancellationToken cancellationToken)
        => ValueTask.FromResult(0m);
}

/// <summary>Stub handler satisfying the compile-time generator requirement for <see cref="OrderConfirmedEvent"/>.</summary>
internal sealed class OrderConfirmedEventStubHandler : INotificationHandler<OrderConfirmedEvent>
{
    public ValueTask Handle(OrderConfirmedEvent notification, CancellationToken cancellationToken)
        => ValueTask.CompletedTask;
}

/// <summary>Stub handler satisfying the compile-time generator requirement for <see cref="PriceHistoryStreamRequest"/>.</summary>
internal sealed class PriceHistoryStreamRequestHandler : IStreamRequestHandler<PriceHistoryStreamRequest, decimal>
{
    public async IAsyncEnumerable<decimal> Handle(
        PriceHistoryStreamRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        yield return 0m;
        await Task.CompletedTask;
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// INotificationBehavior<TNotification> — cross-cutting notification pipeline
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Logging behavior that wraps the notification pipeline.
/// Demonstrates <see cref="INotificationBehavior{TNotification}"/> with
/// struct <see cref="INext"/> continuation — the notification equivalent of
/// <see cref="IPipelineBehavior{TRequest,TResponse}"/>.
/// </summary>
public sealed class NotificationLoggingBehavior<TNotification> : INotificationBehavior<TNotification>
    where TNotification : INotification
{
    /// <inheritdoc />
    public async ValueTask Handle<TNext>(TNotification notification, TNext next, CancellationToken cancellationToken)
        where TNext : struct, INext
    {
        Console.WriteLine($"[NotificationBehavior] Before handling {typeof(TNotification).Name}");
        await next.InvokeAsync().ConfigureAwait(false);
        Console.WriteLine($"[NotificationBehavior] After handling {typeof(TNotification).Name}");
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// DelegateNext<TResponse> — unit-testing IPipelineBehavior without DI
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Demonstrates both constructors of <see cref="DelegateNext{TResponse}"/>:
/// - Async delegate constructor for wrapping real async continuations
/// - Constant result constructor for simple synchronous stubs
/// </summary>
internal static class DelegateNextExamples
{
    /// <summary>
    /// Illustrates using the async constructor: <c>new DelegateNext&lt;T&gt;(asyncFunc)</c>.
    /// </summary>
    public static async Task DemonstrateAsyncConstructor()
    {
        // Arrange: an inline behavior that wraps a Func<ValueTask<T>> continuation
        var behavior = new LoggingPipelineBehavior<DummyRequest, string>();
        var asyncNext = new DelegateNext<string>(() => new ValueTask<string>("async-result"));
        var request = new DummyRequest("event-payload");

        var result = await behavior.Handle(request, asyncNext, CancellationToken.None);
        Console.WriteLine($"   [DelegateNext<T>(Func)] Result: {result}");
    }

    /// <summary>
    /// Illustrates using the constant constructor: <c>new DelegateNext&lt;T&gt;(constantValue)</c>.
    /// </summary>
    public static async Task DemonstrateConstantConstructor()
    {
        var behavior = new LoggingPipelineBehavior<DummyRequest, string>();
        // The constant constructor wraps the value in a completed ValueTask — zero async overhead
        var constantNext = new DelegateNext<string>("constant-result");
        var request = new DummyRequest("payload");

        var result = await behavior.Handle(request, constantNext, CancellationToken.None);
        Console.WriteLine($"   [DelegateNext<T>(constant)] Result: {result}");
    }
}

/// <summary>
/// Minimal request used for isolated behavior unit tests.
/// Not a handler-registration concern — used only in direct behavior invocation.
/// </summary>
public sealed record DummyRequest(string Payload) : ICommand<string>;

/// <summary>Stub handler satisfying the compile-time generator requirement for <see cref="DummyRequest"/>.</summary>
internal sealed class DummyRequestHandler : ICommandHandler<DummyRequest, string>
{
    public ValueTask<string> Handle(DummyRequest command, CancellationToken cancellationToken)
        => ValueTask.FromResult("STUB");
}

/// <summary>Minimal behavior used for DelegateNext unit-test demonstrations.</summary>
public sealed class LoggingPipelineBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
{
    /// <inheritdoc />
    public async ValueTask<TResponse> Handle<TNext>(TRequest request, TNext next, CancellationToken cancellationToken)
        where TNext : struct, INext<TResponse>
    {
        Console.WriteLine($"   [LoggingBehavior] Before → {typeof(TRequest).Name}");
        var response = await next.InvokeAsync().ConfigureAwait(false);
        Console.WriteLine($"   [LoggingBehavior] After ← response received");
        return response;
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// DelegateNext (non-generic, for INotificationBehavior)
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Demonstrates both constructors of the non-generic <see cref="DelegateNext"/>:
/// - Async delegate constructor: <c>new DelegateNext(asyncFunc)</c>
/// - Default no-op constructor: <c>new DelegateNext()</c>
/// </summary>
internal static class DelegateNextNotificationExamples
{
    public static async Task DemonstrateAsyncConstructor()
    {
        var behavior = new NotificationLoggingBehavior<OrderConfirmedEvent>();
        var notification = new OrderConfirmedEvent(Guid.NewGuid());

        // Async Func<ValueTask> constructor
        var asyncNext = new DelegateNext(async () =>
        {
            Console.WriteLine($"   [DelegateNext(Func)] Continuation invoked for {notification.OrderId}");
            await Task.CompletedTask;
        });

        await behavior.Handle(notification, asyncNext, CancellationToken.None);
    }

    public static async Task DemonstrateDefaultConstructor()
    {
        var behavior = new NotificationLoggingBehavior<OrderConfirmedEvent>();
        var notification = new OrderConfirmedEvent(Guid.NewGuid());

        // Default no-op constructor — continuation does nothing (equivalent to Task.CompletedTask)
        var noopNext = new DelegateNext();
        await behavior.Handle(notification, noopNext, CancellationToken.None);
        Console.WriteLine($"   [DelegateNext()] Default no-op continuation completed");
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// FakeMediator — Complete API surface demonstration
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Level 11: Dedicated testing showcase.
/// Covers the complete <see cref="FakeMediator"/> and <see cref="DelegateNext"/> surface.
/// </summary>
public static class Demo
{
    public static async Task RunAsync()
    {
        Console.WriteLine("================================================================================");
        Console.WriteLine("  LEVEL 11: TESTING — FAKEMEDIATOR, DELEGATENEXT & INOTIFICATIONBEHAVIOR");
        Console.WriteLine("================================================================================");

        await DemonstrateINotificationBehavior();
        await DemonstrateDelegateNextGeneric();
        await DemonstrateDelegateNextNonGeneric();
        await DemonstrateFakeMediatorAllOverloads();
        await DemonstrateFakeMediatorAssertions();
        await DemonstrateFakeMediatorReset();
        await DemonstrateFakeAssertionException();

        Console.WriteLine("--------------------------------------------------------------------------------\n");
    }

    // ─── 1. INotificationBehavior<TNotification> ─────────────────────────────

    private static async Task DemonstrateINotificationBehavior()
    {
        Console.WriteLine("1. INotificationBehavior<TNotification> — cross-cutting notification pipeline:");

        var behavior = new NotificationLoggingBehavior<OrderConfirmedEvent>();
        var notification = new OrderConfirmedEvent(Guid.NewGuid());

        // The DelegateNext (non-generic) represents the downstream handler chain
        var next = new DelegateNext(() =>
        {
            Console.WriteLine("   [Downstream handler] OrderConfirmedEvent processed.");
            return ValueTask.CompletedTask;
        });

        await behavior.Handle(notification, next, CancellationToken.None);
        Console.WriteLine();
    }

    // ─── 2. DelegateNext<TResponse> — both constructors ──────────────────────

    private static async Task DemonstrateDelegateNextGeneric()
    {
        Console.WriteLine("2. DelegateNext<TResponse> — both struct constructors:");
        await DelegateNextExamples.DemonstrateAsyncConstructor();
        await DelegateNextExamples.DemonstrateConstantConstructor();
        Console.WriteLine();
    }

    // ─── 3. DelegateNext (non-generic) — both constructors ───────────────────

    private static async Task DemonstrateDelegateNextNonGeneric()
    {
        Console.WriteLine("3. DelegateNext (non-generic for INotificationBehavior) — both struct constructors:");
        await DelegateNextNotificationExamples.DemonstrateAsyncConstructor();
        await DelegateNextNotificationExamples.DemonstrateDefaultConstructor();
        Console.WriteLine();
    }

    // ─── 4. FakeMediator — all Setup overloads ────────────────────────────────

    private static async Task DemonstrateFakeMediatorAllOverloads()
    {
        Console.WriteLine("4. FakeMediator — all SetupCommand/SetupQuery/SetupNotification/SetupStream overloads:");
        var fake = new FakeMediator();

        // 4a. SetupCommand synchronous overload: Func<TCommand, TResponse>
        fake.SetupCommand<PlaceOrderCommand, OrderReceipt>(cmd =>
            new OrderReceipt(Guid.NewGuid(), $"ACCEPTED:{cmd.ProductId}"));
        var orderResult = await fake.Send(new PlaceOrderCommand("PROD-001", 3), CancellationToken.None);
        Console.WriteLine($"   [SetupCommand sync] Status: {orderResult.Status}");

        // 4b. SetupCommand asynchronous overload: Func<TCommand, CancellationToken, ValueTask<TResponse>>
        fake.SetupCommand<PlaceOrderCommand, OrderReceipt>(async (cmd, ct) =>
        {
            await Task.Delay(1, ct);
            return new OrderReceipt(Guid.NewGuid(), $"ASYNC:{cmd.ProductId}");
        });
        var asyncOrderResult = await fake.SendCommand<PlaceOrderCommand, OrderReceipt>(
            new PlaceOrderCommand("PROD-002", 1), CancellationToken.None);
        Console.WriteLine($"   [SetupCommand async] Status: {asyncOrderResult.Status}");

        // 4c. SetupQuery synchronous overload: Func<TQuery, TResponse>
        fake.SetupQuery<GetProductPriceQuery, decimal>(q => q.ProductId == "PROD-001" ? 99.99m : 0m);
        var price = await fake.Send(new GetProductPriceQuery("PROD-001"), CancellationToken.None);
        Console.WriteLine($"   [SetupQuery sync] Price: {price:C}");

        // 4d. SetupQuery asynchronous overload: Func<TQuery, CancellationToken, ValueTask<TResponse>>
        fake.SetupQuery<GetProductPriceQuery, decimal>(async (q, ct) =>
        {
            await Task.Delay(1, ct);
            return 149.99m;
        });
        var asyncPrice = await fake.SendQuery<GetProductPriceQuery, decimal>(
            new GetProductPriceQuery("PROD-002"), CancellationToken.None);
        Console.WriteLine($"   [SetupQuery async] Price: {asyncPrice:C}");

        // 4e. SetupNotification: Func<TNotification, CancellationToken, ValueTask>
        var notificationReceived = false;
        fake.SetupNotification<OrderConfirmedEvent>(async (evt, ct) =>
        {
            await Task.Delay(1, ct);
            notificationReceived = true;
            Console.WriteLine($"   [SetupNotification] Received OrderConfirmedEvent for {evt.OrderId}");
        });
        await fake.Publish(new OrderConfirmedEvent(Guid.NewGuid()), CancellationToken.None);
        Console.WriteLine($"   [SetupNotification] Handler was invoked: {notificationReceived}");

        // 4f. SetupStream: Func<TRequest, CancellationToken, IAsyncEnumerable<TResponse>>
        fake.SetupStream<PriceHistoryStreamRequest, decimal>((req, ct) => GetPriceHistory(req, ct));
        var prices = new List<decimal>();
        await foreach (var p in fake.CreateStream(new PriceHistoryStreamRequest("PROD-001"), CancellationToken.None))
        {
            prices.Add(p);
        }
        Console.WriteLine($"   [SetupStream] Received {prices.Count} price history entries: [{string.Join(", ", prices)}]");

        Console.WriteLine();
    }

    private static async IAsyncEnumerable<decimal> GetPriceHistory(
        PriceHistoryStreamRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        decimal[] history = [89.99m, 94.99m, 99.99m];
        foreach (var p in history)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Delay(1, cancellationToken);
            yield return p;
        }
    }

    // ─── 5. FakeMediator — all assertion methods and history queries ───────────

    private static async Task DemonstrateFakeMediatorAssertions()
    {
        Console.WriteLine("5. FakeMediator — assertion methods and history properties:");
        var fake = new FakeMediator();

        fake.SetupCommand<PlaceOrderCommand, OrderReceipt>(_ => new OrderReceipt(Guid.NewGuid(), "DONE"));
        fake.SetupQuery<GetProductPriceQuery, decimal>(_ => 59.99m);
        fake.SetupNotification<OrderConfirmedEvent>((_, _) => ValueTask.CompletedTask);

        // Dispatch several messages
        await fake.Send(new PlaceOrderCommand("P1", 1), CancellationToken.None);
        await fake.Send(new PlaceOrderCommand("P2", 2), CancellationToken.None);
        await fake.Send(new GetProductPriceQuery("P1"), CancellationToken.None);
        var eventId = Guid.NewGuid();
        await fake.Publish(new OrderConfirmedEvent(eventId), CancellationToken.None);

        // ReceivedRequests — all captured commands and queries
        Console.WriteLine($"   ReceivedRequests.Count = {fake.ReceivedRequests.Count}  (expected: 3)");

        // ReceivedNotifications — all captured notifications
        Console.WriteLine($"   ReceivedNotifications.Count = {fake.ReceivedNotifications.Count}  (expected: 1)");

        // ReceivedRequestsOf<T> — filtered by type
        var orders = fake.ReceivedRequestsOf<PlaceOrderCommand>();
        Console.WriteLine($"   ReceivedRequestsOf<PlaceOrderCommand>().Count = {orders.Count}  (expected: 2)");

        // ReceivedNotificationsOf<T> — filtered by type
        var confirmedEvents = fake.ReceivedNotificationsOf<OrderConfirmedEvent>();
        Console.WriteLine($"   ReceivedNotificationsOf<OrderConfirmedEvent>().Count = {confirmedEvents.Count}  (expected: 1)");

        // ShouldHaveReceived<T>() — assertion: at least one received (no predicate)
        fake.ShouldHaveReceived<PlaceOrderCommand>();
        Console.WriteLine("   ShouldHaveReceived<PlaceOrderCommand>() — PASSED");

        // ShouldHaveReceived<T>(predicate) — assertion: at least one matching
        fake.ShouldHaveReceived<PlaceOrderCommand>(c => c.ProductId == "P1");
        Console.WriteLine("   ShouldHaveReceived<PlaceOrderCommand>(c => c.ProductId == \"P1\") — PASSED");

        // ShouldNotHaveReceived<T>() — assertion: none received
        fake.ShouldNotHaveReceived<FlakyNotification>();
        Console.WriteLine("   ShouldNotHaveReceived<FlakyNotification>() — PASSED");

        // ReceivedCount<T>() — count by type across both requests and notifications
        var count = fake.ReceivedCount<PlaceOrderCommand>();
        Console.WriteLine($"   ReceivedCount<PlaceOrderCommand>() = {count}  (expected: 2)");

        Console.WriteLine();
    }

    // ─── 6. FakeMediator.Reset() ──────────────────────────────────────────────

    private static async Task DemonstrateFakeMediatorReset()
    {
        Console.WriteLine("6. FakeMediator.Reset() — clear all handlers and history:");
        var fake = new FakeMediator();

        fake.SetupCommand<PlaceOrderCommand, OrderReceipt>(_ => new OrderReceipt(Guid.NewGuid(), "BEFORE"));
        await fake.Send(new PlaceOrderCommand("X", 1), CancellationToken.None);

        Console.WriteLine($"   Before Reset — ReceivedRequests.Count = {fake.ReceivedRequests.Count}  (expected: 1)");

        // Reset clears both registered handlers AND the request/notification history
        fake.Reset();

        Console.WriteLine($"   After Reset  — ReceivedRequests.Count = {fake.ReceivedRequests.Count}  (expected: 0)");
        Console.WriteLine("   All handlers cleared. A new SetupXxx call is required before dispatching.");
        Console.WriteLine();
    }

    // ─── 7. FakeAssertionException ────────────────────────────────────────────

    private static Task DemonstrateFakeAssertionException()
    {
        Console.WriteLine("7. FakeAssertionException — thrown when assertions fail:");
        var fake = new FakeMediator();

        try
        {
            // ShouldHaveReceived on a type that was never dispatched → FakeAssertionException
            fake.ShouldHaveReceived<PlaceOrderCommand>();
        }
        catch (FakeAssertionException ex)
        {
            Console.WriteLine($"   Caught FakeAssertionException: {ex.Message}");
        }

        try
        {
            // ShouldNotHaveReceived when the type was NOT in fact received — should pass silently
            fake.ShouldNotHaveReceived<PlaceOrderCommand>();
            Console.WriteLine("   ShouldNotHaveReceived<PlaceOrderCommand>() passed (no exception).");
        }
        catch (FakeAssertionException)
        {
            Console.WriteLine("   Unexpected failure.");
        }

        Console.WriteLine();
        return Task.CompletedTask;
    }
}

/// <summary>Placeholder notification type used to verify ShouldNotHaveReceived assertions.</summary>
internal sealed record FlakyNotification(string Reason) : INotification;

/// <summary>Stub handler satisfying the compile-time generator requirement for <see cref="FlakyNotification"/>.</summary>
internal sealed class FlakyNotificationStubHandler : INotificationHandler<FlakyNotification>
{
    public ValueTask Handle(FlakyNotification notification, CancellationToken cancellationToken)
        => ValueTask.CompletedTask;
}
