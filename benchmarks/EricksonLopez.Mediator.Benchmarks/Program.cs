// Copyright © Erickson Lopez. MIT License.
using System;
using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using EricksonLopez.Mediator;
using EricksonLopez.Mediator.Generated;
using Microsoft.Extensions.DependencyInjection;

namespace EricksonLopez.Mediator.Benchmarks;

// ─── Commands & Queries ───────────────────────────────────────────────────────

/// <summary>
/// Represents PingCommand.
/// </summary>
public class PingCommand : ICommand<string> { }
/// <summary>
/// Represents PingCommandHandler.
/// </summary>
[ServiceLifetime(HandlerLifetime.Singleton)]
public class PingCommandHandler : ICommandHandler<PingCommand, string>
{
    /// <summary>
    /// Executes Handle.
    /// </summary>
    public ValueTask<string> Handle(PingCommand command, CancellationToken cancellationToken) => new("Pong");
}

/// <summary>
/// Represents PingWithOneBehaviorCommand.
/// </summary>
[UseBehavior(typeof(Logging1Behavior))]
public class PingWithOneBehaviorCommand : ICommand<string> { }
/// <summary>
/// Represents PingWithOneBehaviorCommandHandler.
/// </summary>
[ServiceLifetime(HandlerLifetime.Singleton)]
public class PingWithOneBehaviorCommandHandler : ICommandHandler<PingWithOneBehaviorCommand, string>
{
    /// <summary>
    /// Executes Handle.
    /// </summary>
    public ValueTask<string> Handle(PingWithOneBehaviorCommand command, CancellationToken cancellationToken) => new("Pong");
}

/// <summary>
/// Represents PingWithFiveBehaviorsCommand.
/// </summary>
[UseBehavior(typeof(BenchB5), 5)]
[UseBehavior(typeof(BenchB4), 4)]
[UseBehavior(typeof(BenchB3), 3)]
[UseBehavior(typeof(BenchB2), 2)]
[UseBehavior(typeof(BenchB1), 1)]
public class PingWithFiveBehaviorsCommand : ICommand<string> { }
/// <summary>
/// Represents PingWithFiveBehaviorsCommandHandler.
/// </summary>
[ServiceLifetime(HandlerLifetime.Singleton)]
public class PingWithFiveBehaviorsCommandHandler : ICommandHandler<PingWithFiveBehaviorsCommand, string>
{
    /// <summary>
    /// Executes Handle.
    /// </summary>
    public ValueTask<string> Handle(PingWithFiveBehaviorsCommand command, CancellationToken cancellationToken) => new("Pong");
}

/// <summary>
/// Represents GetPingQuery — used to benchmark query dispatch path.
/// </summary>
public class GetPingQuery : IQuery<string> { }
/// <summary>
/// Represents GetPingQueryHandler.
/// </summary>
[ServiceLifetime(HandlerLifetime.Singleton)]
public class GetPingQueryHandler : IQueryHandler<GetPingQuery, string>
{
    /// <summary>
    /// Executes Handle.
    /// </summary>
    public ValueTask<string> Handle(GetPingQuery query, CancellationToken cancellationToken) => new("Pong");
}

/// <summary>
/// Represents MyNotification.
/// </summary>
public class MyNotification : INotification { }
/// <summary>
/// Represents MyNotificationHandler1.
/// </summary>
public class MyNotificationHandler1 : INotificationHandler<MyNotification>
{
    /// <summary>
    /// Executes Handle.
    /// </summary>
    public ValueTask Handle(MyNotification notification, CancellationToken cancellationToken) => default;
}

/// <summary>
/// Represents ManyNotification.
/// </summary>
public class ManyNotification : INotification { }
/// <summary>
/// Represents ManyNotificationHandler1.
/// </summary>
public class ManyNotificationHandler1 : INotificationHandler<ManyNotification>
{
    /// <summary>
    /// Executes Handle.
    /// </summary>
    public ValueTask Handle(ManyNotification notification, CancellationToken cancellationToken) => default;
}
/// <summary>
/// Represents ManyNotificationHandler2.
/// </summary>
public class ManyNotificationHandler2 : INotificationHandler<ManyNotification>
{
    /// <summary>
    /// Executes Handle.
    /// </summary>
    public ValueTask Handle(ManyNotification notification, CancellationToken cancellationToken) => default;
}
/// <summary>
/// Represents ManyNotificationHandler3.
/// </summary>
public class ManyNotificationHandler3 : INotificationHandler<ManyNotification>
{
    /// <summary>
    /// Executes Handle.
    /// </summary>
    public ValueTask Handle(ManyNotification notification, CancellationToken cancellationToken) => default;
}
/// <summary>
/// Represents ManyNotificationHandler4.
/// </summary>
public class ManyNotificationHandler4 : INotificationHandler<ManyNotification>
{
    /// <summary>
    /// Executes Handle.
    /// </summary>
    public ValueTask Handle(ManyNotification notification, CancellationToken cancellationToken) => default;
}
/// <summary>
/// Represents ManyNotificationHandler5.
/// </summary>
public class ManyNotificationHandler5 : INotificationHandler<ManyNotification>
{
    /// <summary>
    /// Executes Handle.
    /// </summary>
    public ValueTask Handle(ManyNotification notification, CancellationToken cancellationToken) => default;
}

/// <summary>
/// Represents ParallelNotification — uses Parallel publish strategy.
/// Validates AsTask allocation path vs sequential.
/// </summary>
[PublishStrategy(PublishStrategy.Parallel)]
public class ParallelNotification : INotification { }
/// <summary>
/// Represents ParallelNotificationHandler1.
/// </summary>
public class ParallelNotificationHandler1 : INotificationHandler<ParallelNotification>
{
    /// <summary>
    /// Executes Handle.
    /// </summary>
    public ValueTask Handle(ParallelNotification notification, CancellationToken cancellationToken) => default;
}
/// <summary>
/// Represents ParallelNotificationHandler2.
/// </summary>
public class ParallelNotificationHandler2 : INotificationHandler<ParallelNotification>
{
    /// <summary>
    /// Executes Handle.
    /// </summary>
    public ValueTask Handle(ParallelNotification notification, CancellationToken cancellationToken) => default;
}
/// <summary>
/// Represents ParallelNotificationHandler3.
/// </summary>
public class ParallelNotificationHandler3 : INotificationHandler<ParallelNotification>
{
    /// <summary>
    /// Executes Handle.
    /// </summary>
    public ValueTask Handle(ParallelNotification notification, CancellationToken cancellationToken) => default;
}

/// <summary>
/// Represents OuterCommand.
/// </summary>
public class OuterCommand : ICommand<string> { }
/// <summary>
/// Represents OuterCommandHandler.
/// </summary>
[ServiceLifetime(HandlerLifetime.Singleton)]
public class OuterCommandHandler : ICommandHandler<OuterCommand, string>
{
    private readonly IMediator _mediator;
    public OuterCommandHandler(IMediator mediator) => _mediator = mediator;
    /// <summary>
    /// Executes Handle.
    /// </summary>
    public async ValueTask<string> Handle(OuterCommand command, CancellationToken cancellationToken)
    {
        return await _mediator.Send(new PingCommand(), cancellationToken);
    }
}

// ─── Behaviors ────────────────────────────────────────────────────────────────

/// <summary>
/// Represents Logging1Behavior.
/// </summary>
public class Logging1Behavior : IPipelineBehavior<PingWithOneBehaviorCommand, string>
{
    /// <summary>
    /// Executes Handle.
    /// </summary>
    public async ValueTask<string> Handle<TNext>(PingWithOneBehaviorCommand request, TNext next, CancellationToken cancellationToken)
        where TNext : struct, INext<string>
        => await next.InvokeAsync();
}

/// <summary>
/// Represents BenchB1.
/// </summary>
public class BenchB1 : IPipelineBehavior<PingWithFiveBehaviorsCommand, string>
{
    /// <summary>
    /// Executes Handle.
    /// </summary>
    public async ValueTask<string> Handle<TNext>(PingWithFiveBehaviorsCommand request, TNext next, CancellationToken cancellationToken)
        where TNext : struct, INext<string> => await next.InvokeAsync();
}
/// <summary>
/// Represents BenchB2.
/// </summary>
public class BenchB2 : IPipelineBehavior<PingWithFiveBehaviorsCommand, string>
{
    /// <summary>
    /// Executes Handle.
    /// </summary>
    public async ValueTask<string> Handle<TNext>(PingWithFiveBehaviorsCommand request, TNext next, CancellationToken cancellationToken)
        where TNext : struct, INext<string> => await next.InvokeAsync();
}
/// <summary>
/// Represents BenchB3.
/// </summary>
public class BenchB3 : IPipelineBehavior<PingWithFiveBehaviorsCommand, string>
{
    /// <summary>
    /// Executes Handle.
    /// </summary>
    public async ValueTask<string> Handle<TNext>(PingWithFiveBehaviorsCommand request, TNext next, CancellationToken cancellationToken)
        where TNext : struct, INext<string> => await next.InvokeAsync();
}
/// <summary>
/// Represents BenchB4.
/// </summary>
public class BenchB4 : IPipelineBehavior<PingWithFiveBehaviorsCommand, string>
{
    /// <summary>
    /// Executes Handle.
    /// </summary>
    public async ValueTask<string> Handle<TNext>(PingWithFiveBehaviorsCommand request, TNext next, CancellationToken cancellationToken)
        where TNext : struct, INext<string> => await next.InvokeAsync();
}
/// <summary>
/// Represents BenchB5.
/// </summary>
public class BenchB5 : IPipelineBehavior<PingWithFiveBehaviorsCommand, string>
{
    /// <summary>
    /// Executes Handle.
    /// </summary>
    public async ValueTask<string> Handle<TNext>(PingWithFiveBehaviorsCommand request, TNext next, CancellationToken cancellationToken)
        where TNext : struct, INext<string> => await next.InvokeAsync();
}

// ─── Direct handler (for baseline comparison) ────────────────────────────────

/// <summary>
/// Represents DirectPingCommandHandler.
/// </summary>
public class DirectPingCommandHandler
{
    /// <summary>
    /// Executes Handle.
    /// </summary>
    public ValueTask<string> Handle(PingCommand command, CancellationToken cancellationToken) => new("Pong");
}

// ─── Benchmarks ───────────────────────────────────────────────────────────────

/// <summary>
/// BenchmarkDotNet scenarios covering the Fase 1 performance objectives:
/// - Send (0 behaviors): ≤ 5ns overhead over direct call
/// - Allocations (0 behaviors): 0 bytes extra
/// - Send (1 behavior): ≤ 10ns overhead
/// - Allocations (1 behavior): 0 bytes extra (synchronous path)
/// </summary>
[MemoryDiagnoser]
[ShortRunJob]
public class MediatorBenchmarks
{
    private IMediator _mediator = null!;
    private readonly PingCommand _pingCommand = new();
    private readonly GetPingQuery _getPingQuery = new();
    private readonly PingWithOneBehaviorCommand _oneBehaviorCommand = new();
    private readonly PingWithFiveBehaviorsCommand _fiveBehaviorsCommand = new();
    private readonly MyNotification _notification = new();
    private readonly ManyNotification _manyNotification = new();
    private readonly ParallelNotification _parallelNotification = new();
    private readonly OuterCommand _outerCommand = new();
    private readonly DirectPingCommandHandler _directHandler = new();

    /// <summary>
    /// Executes Setup.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        var services = new ServiceCollection();
        services.AddEricksonLopezMediator();
        // Register behavior dependencies as singleton for benchmark stability
        services.AddSingleton<Logging1Behavior>();
        services.AddSingleton<BenchB1>();
        services.AddSingleton<BenchB2>();
        services.AddSingleton<BenchB3>();
        services.AddSingleton<BenchB4>();
        services.AddSingleton<BenchB5>();
        var sp = services.BuildServiceProvider();
        _mediator = sp.GetRequiredService<IMediator>();
    }

    /// <summary>Baseline: direct method call bypassing all DI and dispatch.</summary>
    [Benchmark(Baseline = true)]
    public ValueTask<string> DirectCall()
        => _directHandler.Handle(_pingCommand, CancellationToken.None);

    /// <summary>Send command with 0 behaviors — target: ≤ 5ns overhead over DirectCall.</summary>
    [Benchmark]
    public ValueTask<string> SendCommand_NoBehaviors()
        => _mediator.Send(_pingCommand, CancellationToken.None);

    /// <summary>Send query with 0 behaviors — validates query dispatch branch.</summary>
    [Benchmark]
    public ValueTask<string> SendQuery_NoBehaviors()
        => _mediator.Send(_getPingQuery, CancellationToken.None);

    /// <summary>Send command with 1 behavior — target: ≤ 10ns overhead, 0 allocations.</summary>
    [Benchmark]
    public ValueTask<string> SendCommand_OneBehavior()
        => _mediator.Send(_oneBehaviorCommand, CancellationToken.None);

    /// <summary>Send command with 5 behaviors — measures pipeline scaling.</summary>
    [Benchmark]
    public ValueTask<string> SendCommand_FiveBehaviors()
        => _mediator.Send(_fiveBehaviorsCommand, CancellationToken.None);

    /// <summary>Publish notification to 1 handler (sequential).</summary>
    [Benchmark]
    public ValueTask PublishNotification_OneHandler()
        => _mediator.Publish(_notification, CancellationToken.None);

    /// <summary>Publish notification to 5 handlers (sequential).</summary>
    [Benchmark]
    public ValueTask PublishNotification_FiveHandlers()
        => _mediator.Publish(_manyNotification, CancellationToken.None);

    /// <summary>Publish to 3 parallel handlers — validates Task.WhenAll path (AsTask allocation).</summary>
    [Benchmark]
    public ValueTask PublishNotification_Parallel()
        => _mediator.Publish(_parallelNotification, CancellationToken.None);

    /// <summary>Nested send: outer handler dispatches inner command via IMediator.</summary>
    [Benchmark]
    public ValueTask<string> NestedSend()
        => _mediator.Send(_outerCommand, CancellationToken.None);
}

/// <summary>
/// Represents Program.
/// </summary>
public static class Program
{
    /// <summary>
    /// Executes Main.
    /// </summary>
    public static void Main(string[] args)
    {
        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
    }
}




