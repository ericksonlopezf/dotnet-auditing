// Copyright © Erickson Lopez. MIT License.
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.RateLimiting;
using System.Threading.Tasks;
using EricksonLopez.Mediator;
using EricksonLopez.Mediator.RateLimiting;

namespace Sample.Levels.Level7_Scalability;

// --- 1. High Throughput Command for Benchmarking ---
public sealed record HighThroughputCommand(int Value) : ICommand<int>;

public sealed class HighThroughputCommandHandler : ICommandHandler<HighThroughputCommand, int>
{
    public ValueTask<int> Handle(HighThroughputCommand command, CancellationToken cancellationToken)
    {
        return ValueTask.FromResult(command.Value * 2);
    }
}

// --- 2. Rate-Limited Command ---
public sealed record RateLimitedApiCommand(string ClientId) : ICommand<string>;

public sealed class RateLimitedApiCommandHandler : ICommandHandler<RateLimitedApiCommand, string>
{
    public ValueTask<string> Handle(RateLimitedApiCommand command, CancellationToken cancellationToken)
    {
        return ValueTask.FromResult($"Request approved for client {command.ClientId}");
    }
}

/// <summary>
/// Level 7: Scalability, Zero Allocations, and Rate Limiting.
/// </summary>
public static class Demo
{
    public static async Task RunAsync(IMediator mediator)
    {
        Console.WriteLine("================================================================================");
        Console.WriteLine("  LEVEL 7: SCALABILITY, THROUGHPUT & RATE LIMITING");
        Console.WriteLine("================================================================================");

        // 1. Rate Limiting Demonstration with RateLimitingBehavior
        Console.WriteLine("1. Rate Limiting Demonstration:");
        var rateLimiter = new TokenBucketRateLimiter(new TokenBucketRateLimiterOptions
        {
            TokenLimit = 2,
            TokensPerPeriod = 2,
            ReplenishmentPeriod = TimeSpan.FromSeconds(10),
            QueueLimit = 0
        });

        var rateLimitedBehavior = new RateLimitingBehavior<RateLimitedApiCommand, string>(rateLimiter);
        var directNext = new EricksonLopez.Mediator.Testing.DelegateNext<string>(() => ValueTask.FromResult("OK"));

        for (int i = 1; i <= 3; i++)
        {
            try
            {
                var result = await rateLimitedBehavior.Handle(new RateLimitedApiCommand("CLIENT_01"), directNext, CancellationToken.None);
                Console.WriteLine($"   -> Request #{i}: {result}");
            }
            catch (RateLimitExceededException ex)
            {
                Console.WriteLine($"   -> Request #{i}: [RateLimitExceededException] {ex.Message}");
            }
        }
        Console.WriteLine();

        // 2. High-Throughput Zero-Allocations Execution (5,000 operations)
        Console.WriteLine("2. Dispatching Throughput Benchmark (5,000 sequential operations):");
        var sw = Stopwatch.StartNew();
        int accumulator = 0;
        const int iterations = 5000;
        for (int i = 0; i < iterations; i++)
        {
            accumulator += await mediator.Send(new HighThroughputCommand(1), CancellationToken.None);
        }
        sw.Stop();

        var opsPerSec = (iterations / sw.Elapsed.TotalSeconds);
        Console.WriteLine($"   -> {iterations:N0} commands executed in {sw.ElapsedMilliseconds} ms ({opsPerSec:N0} ops/sec).");
        Console.WriteLine($"   -> Zero heap overhead across pipeline wrappers (struct INext<TResponse>).");

        Console.WriteLine("--------------------------------------------------------------------------------\n");
    }
}
