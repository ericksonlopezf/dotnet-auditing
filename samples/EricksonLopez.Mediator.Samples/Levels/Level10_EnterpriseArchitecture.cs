// Copyright © Erickson Lopez. MIT License.
using System;
using System.Threading;
using System.Threading.Tasks;
using EricksonLopez.Mediator;

namespace Sample.Levels.Level10_EnterpriseArchitecture;

// --- 1. Domain & Microservice Contracts ---
public sealed record ServerlessProcessCommand(string Payload) : ICommand<string>;

public sealed class ServerlessProcessCommandHandler : ICommandHandler<ServerlessProcessCommand, string>
{
    public ValueTask<string> Handle(ServerlessProcessCommand command, CancellationToken cancellationToken)
    {
        return ValueTask.FromResult($"PROCESSED: {command.Payload.ToUpperInvariant()}");
    }
}

public sealed record ServerlessStatusQuery(string ServiceKey) : IQuery<bool>;

public sealed class ServerlessStatusQueryHandler : IQueryHandler<ServerlessStatusQuery, bool>
{
    public ValueTask<bool> Handle(ServerlessStatusQuery query, CancellationToken cancellationToken)
    {
        return ValueTask.FromResult(true);
    }
}

// --- 2. Multi-Layer Enterprise Event ---
public sealed record EnterpriseDomainEvent(Guid AggregateId, string EventType) : INotification;

public sealed class OutboxPatternNotificationHandler : INotificationHandler<EnterpriseDomainEvent>
{
    public ValueTask Handle(EnterpriseDomainEvent notification, CancellationToken cancellationToken)
    {
        Console.WriteLine($"[Level 10 - Outbox] Persisting event '{notification.EventType}' to Transactional Outbox table (ID: {notification.AggregateId})");
        return ValueTask.CompletedTask;
    }
}

public sealed class KafkaMessageRelayNotificationHandler : INotificationHandler<EnterpriseDomainEvent>
{
    public ValueTask Handle(EnterpriseDomainEvent notification, CancellationToken cancellationToken)
    {
        Console.WriteLine($"[Level 10 - Broker] Relaying event '{notification.EventType}' to distributed message bus");
        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// Level 10: Enterprise Architecture & StaticMediator (Zero-DI Container AOT Execution).
/// </summary>
public static class Demo
{
    public static async Task RunAsync(IMediator mediator)
    {
        Console.WriteLine("================================================================================");
        Console.WriteLine("  LEVEL 10: ENTERPRISE ARCHITECTURE & STATICMEDIATOR (ZERO-DI)");
        Console.WriteLine("================================================================================");

        // 1. StaticMediator: Static dispatch without DI Container (AWS Lambda, Azure Functions, Cloudflare Workers, IoT)
        Console.WriteLine("1. Using StaticMediator for Ultra-Low Latency Serverless Environments (Zero-DI):");
        StaticMediator.Reset();

        // Manual static registration
        StaticMediator.RegisterCommandHandler(new ServerlessProcessCommandHandler());
        StaticMediator.RegisterQueryHandler(new ServerlessStatusQueryHandler());
        StaticMediator.RegisterNotificationHandler(new ServerlessEventAuditHandler());

        // Direct and strongly typed static invocation
        var cmdRes = await StaticMediator.SendCommand<ServerlessProcessCommand, string>(
            new ServerlessProcessCommand("cold-start-elimination"), CancellationToken.None);
        var qryRes = await StaticMediator.SendQuery<ServerlessStatusQuery, bool>(
            new ServerlessStatusQuery("PaymentGateway"), CancellationToken.None);

        // StaticMediator.Publish — notification fan-out without DI
        await StaticMediator.Publish(
            new ServerlessFunctionInvokedEvent("HandleOrder", DateTime.UtcNow), CancellationToken.None);

        Console.WriteLine($"   -> StaticMediator Command Result: {cmdRes}");
        Console.WriteLine($"   -> StaticMediator Query Result: {qryRes}");
        Console.WriteLine($"   -> StaticMediator.Publish dispatched ServerlessFunctionInvokedEvent");
        Console.WriteLine();

        // 2. Enterprise Event Architecture & Outbox Pattern
        Console.WriteLine("2. Multi-Layer Domain Event Orchestration (Clean Architecture & Outbox):");
        var aggregateId = Guid.NewGuid();
        var domainEvent = new EnterpriseDomainEvent(aggregateId, "AccountBalanceAdjusted");
        await mediator.Publish(domainEvent, CancellationToken.None);

        Console.WriteLine("--------------------------------------------------------------------------------\n");
    }
}

/// <summary>Serverless domain event demonstrating StaticMediator.Publish.</summary>
public sealed record ServerlessFunctionInvokedEvent(string FunctionName, DateTime InvokedAt) : INotification;

/// <summary>Audit handler registered via StaticMediator.RegisterNotificationHandler.</summary>
public sealed class ServerlessEventAuditHandler : INotificationHandler<ServerlessFunctionInvokedEvent>
{
    public ValueTask Handle(ServerlessFunctionInvokedEvent notification, CancellationToken cancellationToken)
    {
        Console.WriteLine($"   [StaticMediator Notification] Function '{notification.FunctionName}' invoked at {notification.InvokedAt:HH:mm:ss}");
        return ValueTask.CompletedTask;
    }
}

