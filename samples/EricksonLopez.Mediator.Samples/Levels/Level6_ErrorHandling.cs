// Copyright © Erickson Lopez. MIT License.
using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using EricksonLopez.Mediator;
using EricksonLopez.Mediator.Polly;
using Polly;

namespace Sample.Levels.Level6_ErrorHandling;

// --- 1. Flaky Command with Polly v8 Resilience Pipeline ---
[UseResiliencePipeline("SampleRetryPipeline")]
public sealed record FlakyExternalServiceCommand(string Endpoint) : ICommand<string>;

public sealed class FlakyExternalServiceCommandHandler : ICommandHandler<FlakyExternalServiceCommand, string>
{
    private static int _attemptCount = 0;

    public ValueTask<string> Handle(FlakyExternalServiceCommand command, CancellationToken cancellationToken)
    {
        _attemptCount++;
        Console.WriteLine($"[Level 6 - Handler] Attempt #{_attemptCount} connecting to '{command.Endpoint}'...");
        if (_attemptCount < 2)
        {
            throw new HttpRequestException("Transient network failure (503 Service Unavailable).");
        }

        Console.WriteLine($"[Level 6 - Handler] Connection successfully established on attempt #{_attemptCount}.");
        return ValueTask.FromResult("HTTP 200 OK");
    }
}

// --- 2. Notification with Exception Aggregation Strategy ---
[PublishStrategy(PublishStrategy.SequentialAggregateExceptions)]
public sealed record CriticalSystemEvent(string AlertMessage) : INotification;

public sealed class HandlerThatFails1 : INotificationHandler<CriticalSystemEvent>
{
    public ValueTask Handle(CriticalSystemEvent notification, CancellationToken cancellationToken)
    {
        Console.WriteLine("[Level 6 - Handler 1] Processing alert... -> Failing intentionally.");
        throw new InvalidOperationException("Handler 1: Unable to connect to messaging service.");
    }
}

public sealed class HandlerThatSucceeds : INotificationHandler<CriticalSystemEvent>
{
    public ValueTask Handle(CriticalSystemEvent notification, CancellationToken cancellationToken)
    {
        Console.WriteLine("[Level 6 - Handler 2] Processing alert... -> Completed successfully.");
        return ValueTask.CompletedTask;
    }
}

public sealed class HandlerThatFails2 : INotificationHandler<CriticalSystemEvent>
{
    public ValueTask Handle(CriticalSystemEvent notification, CancellationToken cancellationToken)
    {
        Console.WriteLine("[Level 6 - Handler 3] Processing alert... -> Failing intentionally.");
        throw new TimeoutException("Handler 3: Timeout writing to secondary database replica.");
    }
}

/// <summary>
/// Level 6: Error Handling, Resilience with Polly v8, and Exception Aggregation.
/// </summary>
public static class Demo
{
    public static async Task RunAsync(IMediator mediator)
    {
        Console.WriteLine("================================================================================");
        Console.WriteLine("  LEVEL 6: ERROR HANDLING, POLLY RESILIENCE & EXCEPTION AGGREGATION");
        Console.WriteLine("================================================================================");

        // 1. Resilience and Retries with Polly v8
        Console.WriteLine("1. Execution with Polly Resilience Pipeline ([UseResiliencePipeline]):");
        try
        {
            var res = await mediator.Send(new FlakyExternalServiceCommand("https://api.external.com/v1"), CancellationToken.None);
            Console.WriteLine($"   -> Result after retries: {res}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"   -> Non-recoverable exception: {ex.Message}");
        }
        Console.WriteLine();

        // 2. Publishing with Exception Aggregation Strategy
        Console.WriteLine("2. Publishing with [PublishStrategy(PublishStrategy.SequentialAggregateExceptions)]:");
        try
        {
            var alertEvent = new CriticalSystemEvent("High CPU alert on node 3");
            await mediator.Publish(alertEvent, CancellationToken.None);
        }
        catch (NotificationHandlerAggregateException aggEx)
        {
            Console.WriteLine($"   -> Caught NotificationHandlerAggregateException with {aggEx.HandlerExceptions.Count} exception(s):");
            foreach (var inner in aggEx.HandlerExceptions)
            {
                Console.WriteLine($"      * [{inner.GetType().Name}]: {inner.Message}");
            }
        }

        Console.WriteLine("--------------------------------------------------------------------------------\n");
    }
}
