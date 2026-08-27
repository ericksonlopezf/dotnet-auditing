// Copyright © Erickson Lopez. MIT License.
using System;
using System.Collections.Concurrent;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using EricksonLopez.Mediator;

namespace EricksonLopez.Mediator.Tests.Fixtures;

// ─── Static Commands & Queries ───────────────────────────────────────────────

public record StaticPingCommand(string Message) : ICommand<string>;

public class StaticPingCommandHandler : ICommandHandler<StaticPingCommand, string>
{
    public ValueTask<string> Handle(StaticPingCommand command, CancellationToken cancellationToken)
        => ValueTask.FromResult($"StaticPong: {command.Message}");
}

public record StaticGetQuery(int Id) : IQuery<int>;

public class StaticGetQueryHandler : IQueryHandler<StaticGetQuery, int>
{
    public ValueTask<int> Handle(StaticGetQuery query, CancellationToken cancellationToken)
        => ValueTask.FromResult(query.Id * 10);
}

// ─── Static Notifications ───────────────────────────────────────────────────

public record StaticNotification(string EventName) : INotification;

public class StaticNotificationHandler : INotificationHandler<StaticNotification>
{
    private int _handledCount;
    public int HandledCount => _handledCount;

    public ValueTask Handle(StaticNotification notification, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _handledCount);
        return default;
    }
}

public class StaticCancellableNotificationHandler : INotificationHandler<StaticNotification>
{
    public CancellationToken ReceivedToken { get; private set; }

    public ValueTask Handle(StaticNotification notification, CancellationToken cancellationToken)
    {
        ReceivedToken = cancellationToken;
        return default;
    }
}

// ─── Non-Conforming Handlers for Edge-Case Testing ──────────────────────────

public class WrongReturnTypeCommandHandler
{
    public int Handle(StaticPingCommand command, CancellationToken cancellationToken) => 42;
}

public class WrongReturnTypeQueryHandler
{
    public string Handle(StaticGetQuery query, CancellationToken cancellationToken) => "Wrong";
}

// ─── Static Mediator Test Helper ────────────────────────────────────────────

/// <summary>
/// Internal test helper for StaticMediator testing.
/// Encapsulates reflection-based injection used exclusively to verify runtime defenses
/// against corrupt registry states or non-conforming handler objects that cannot be registered
/// through the strongly-typed generic API.
/// </summary>
public static class StaticMediatorTestHelper
{
    public static void InjectRawCommandHandler(Type commandType, object handler)
    {
        var field = typeof(StaticMediator).GetField("CommandHandlers", BindingFlags.NonPublic | BindingFlags.Static)!;
        var dict = (ConcurrentDictionary<Type, object>)field.GetValue(null)!;
        dict[commandType] = handler;
    }

    public static void InjectRawQueryHandler(Type queryType, object handler)
    {
        var field = typeof(StaticMediator).GetField("QueryHandlers", BindingFlags.NonPublic | BindingFlags.Static)!;
        var dict = (ConcurrentDictionary<Type, object>)field.GetValue(null)!;
        dict[queryType] = handler;
    }
}
