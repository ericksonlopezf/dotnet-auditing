// Copyright © Erickson Lopez. MIT License.
using System;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using EricksonLopez.Mediator;
using EricksonLopez.Mediator.Polly;
using EricksonLopez.Mediator.Testing;
using global::Polly;
using global::Polly.Registry;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EricksonLopez.Mediator.Polly.Tests;

public record PollyPingCommand(string Value) : ICommand<string>;
public class PollyPingCommandHandler : ICommandHandler<PollyPingCommand, string>
{
    public ValueTask<string> Handle(PollyPingCommand command, CancellationToken cancellationToken)
        => ValueTask.FromResult("PollyPong: " + command.Value);
}

[UseResiliencePipeline("CustomKey")]
public record PollyKeyedCommand(string Value) : ICommand<string>;
public class PollyKeyedCommandHandler : ICommandHandler<PollyKeyedCommand, string>
{
    public ValueTask<string> Handle(PollyKeyedCommand command, CancellationToken cancellationToken)
        => ValueTask.FromResult("KeyedPong: " + command.Value);
}

public class PollyBehaviorTests
{
    [Fact]
    public async Task Handle_WithoutPipeline_ExecutesNormally()
    {
        var behavior = new PollyResilienceBehavior<PollyPingCommand, string>();
        var command = new PollyPingCommand("Test");
        var next = new DelegateNext<string>(() => ValueTask.FromResult("Pong: " + command.Value));

        var result = await behavior.Handle(command, next, CancellationToken.None);

        result.Should().Be("Pong: Test");
    }

    [Fact]
    public async Task Handle_WithDefaultPipeline_AppliesRetry()
    {
        int attempt = 0;
        var pipeline = BuildRetryPipeline(maxRetryAttempts: 3);

        var behavior = new PollyResilienceBehavior<PollyPingCommand, string>(defaultPipeline: pipeline);
        var command = new PollyPingCommand("RetryTest");
        var next = new DelegateNext<string>(() =>
        {
            attempt++;
            if (attempt < 3)
            {
                throw new InvalidOperationException("Temporary failure");
            }
            return ValueTask.FromResult("Success on attempt " + attempt);
        });

        var result = await behavior.Handle(command, next, CancellationToken.None);

        result.Should().Be("Success on attempt 3");
        attempt.Should().Be(3);
    }

    [Fact]
    public async Task Handle_WhenAllRetriesExhausted_PropagatesOriginalExceptionAndExecutesExpectedAttempts()
    {
        int attempt = 0;
        var pipeline = BuildRetryPipeline(maxRetryAttempts: 3);

        var behavior = new PollyResilienceBehavior<PollyPingCommand, string>(defaultPipeline: pipeline);
        var command = new PollyPingCommand("ExhaustionTest");
        var next = new DelegateNext<string>(() =>
        {
            attempt++;
            throw new InvalidOperationException("Permanent failure on attempt " + attempt);
        });

        var act = async () => await behavior.Handle(command, next, CancellationToken.None).AsTask();

        var ex = await act.Should().ThrowAsync<InvalidOperationException>();
        ex.Which.Message.Should().Contain("Permanent failure");
        attempt.Should().Be(4, "1 initial attempt + 3 retries = 4 total attempts");
    }

    [Fact]
    public async Task Handle_WithKeyedPipelineProvider_ResolvesKeyedPipeline()
    {
        int attempts = 0;
        var registry = new ResiliencePipelineRegistry<string>();
        registry.TryAddBuilder("CustomKey", (builder, _) =>
        {
            builder.AddRetry(new()
            {
                MaxRetryAttempts = 2,
                Delay = TimeSpan.Zero
            });
        });

        var behavior = new PollyResilienceBehavior<PollyKeyedCommand, string>(pipelineProvider: registry);
        var command = new PollyKeyedCommand("KeyedTest");
        var next = new DelegateNext<string>(() =>
        {
            attempts++;
            if (attempts < 2)
            {
                throw new InvalidOperationException("Fail once");
            }
            return ValueTask.FromResult("KeyedSuccess");
        });

        var result = await behavior.Handle(command, next, CancellationToken.None);

        result.Should().Be("KeyedSuccess");
        attempts.Should().Be(2);
    }

    [Fact]
    public void AddMediatorPolly_ValidServices_RegistersServicesCorrectly()
    {
        var services = new ServiceCollection();
        services.AddMediatorPolly();

        var sp = services.BuildServiceProvider();
        var behavior = sp.GetService<PollyResilienceBehavior<PollyPingCommand, string>>();

        behavior.Should().NotBeNull();
    }

    [Fact]
    public async Task AddMediatorDefaultResiliencePipeline_ConfiguresBuilderAndRegistersServices()
    {
        var services = new ServiceCollection();
        var configureCalled = false;
        services.AddMediatorDefaultResiliencePipeline(b =>
        {
            configureCalled = true;
            b.AddRetry(new()
            {
                MaxRetryAttempts = 2,
                Delay = TimeSpan.Zero
            });
        });

        configureCalled.Should().BeTrue();

        var sp = services.BuildServiceProvider();
        var pipeline = sp.GetService<ResiliencePipeline>();
        var behavior = sp.GetService<PollyResilienceBehavior<PollyPingCommand, string>>();

        pipeline.Should().NotBeNull();
        behavior.Should().NotBeNull();

        // Verify the configured pipeline actually executes with retries
        int attempts = 0;
        await pipeline!.ExecuteAsync(async _ =>
        {
            attempts++;
            if (attempts < 2)
            {
                throw new InvalidOperationException("Fail once");
            }
            await ValueTask.CompletedTask;
        });

        attempts.Should().Be(2);
    }

    [Fact]
    public void AddMediatorDefaultResiliencePipeline_NullConfigure_ThrowsArgumentNullException()
    {
        var services = new ServiceCollection();
        var act = () => services.AddMediatorDefaultResiliencePipeline(null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("configure");
    }

    [Fact]
    public void Constructor_UseResiliencePipelineAttribute_InitializesAndThrowsOnNull()
    {
        var attr = new UseResiliencePipelineAttribute("CustomKey");
        attr.PipelineKey.Should().Be("CustomKey");

        var act = () => new UseResiliencePipelineAttribute(null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("pipelineKey");
    }

    [Fact]
    public async Task Handle_WithDefaultInRegistry_ResolvesDefaultRegistryPipeline()
    {
        int attempts = 0;
        var registry = new ResiliencePipelineRegistry<string>();
        registry.TryAddBuilder("Default", (builder, _) =>
        {
            builder.AddRetry(new()
            {
                MaxRetryAttempts = 2,
                Delay = TimeSpan.Zero
            });
        });

        var behavior = new PollyResilienceBehavior<PollyPingCommand, string>(pipelineProvider: registry);
        var command = new PollyPingCommand("DefaultRegistryTest");
        var next = new DelegateNext<string>(() =>
        {
            attempts++;
            if (attempts < 2)
            {
                throw new InvalidOperationException("Fail once for default registry");
            }
            return ValueTask.FromResult("DefaultSuccess");
        });

        var result = await behavior.Handle(command, next, CancellationToken.None);

        result.Should().Be("DefaultSuccess");
        attempts.Should().Be(2);
    }

    [Fact]
    public async Task Handle_KeyNotFoundInRegistry_FallsBackToDefaultPipeline()
    {
        int attempts = 0;
        var registry = new ResiliencePipelineRegistry<string>();
        var defaultPipeline = BuildRetryPipeline(maxRetryAttempts: 2);

        var behavior = new PollyResilienceBehavior<PollyKeyedCommand, string>(
            pipelineProvider: registry,
            defaultPipeline: defaultPipeline);

        var command = new PollyKeyedCommand("FallbackTest");
        var next = new DelegateNext<string>(() =>
        {
            attempts++;
            if (attempts < 2)
            {
                throw new InvalidOperationException("Fail once for fallback");
            }
            return ValueTask.FromResult("FallbackSuccess");
        });

        var result = await behavior.Handle(command, next, CancellationToken.None);

        result.Should().Be("FallbackSuccess");
        attempts.Should().Be(2);
    }

    [Fact]
    public async Task Handle_WithCancellationToken_HonorsCancellation()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var pipeline = BuildRetryPipeline(maxRetryAttempts: 2, delay: TimeSpan.FromMilliseconds(10));

        var behavior = new PollyResilienceBehavior<PollyPingCommand, string>(defaultPipeline: pipeline);
        var command = new PollyPingCommand("CancelTest");
        var next = new DelegateNext<string>(() => throw new OperationCanceledException(cts.Token));

        var act = async () => await behavior.Handle(command, next, cts.Token).AsTask();
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public void AddMediatorPolly_NullServices_ThrowsArgumentNullException()
    {
        IServiceCollection services = null!;
        var act = () => services.AddMediatorPolly();
        act.Should().Throw<ArgumentNullException>().WithParameterName("services");
    }

    [Fact]
    public void AddMediatorDefaultResiliencePipeline_NullServices_ThrowsArgumentNullException()
    {
        IServiceCollection services = null!;
        var act = () => services.AddMediatorDefaultResiliencePipeline(b => { });
        act.Should().Throw<ArgumentNullException>().WithParameterName("services");
    }

    [Fact]
    public async Task Handle_KeyedCommand_WhenProviderIsNull_FallsBackToDefaultPipeline()
    {
        int attempts = 0;
        var defaultPipeline = BuildRetryPipeline(maxRetryAttempts: 2);

        var behavior = new PollyResilienceBehavior<PollyKeyedCommand, string>(
            pipelineProvider: null,
            defaultPipeline: defaultPipeline);

        var command = new PollyKeyedCommand("NoProviderFallback");
        var next = new DelegateNext<string>(() =>
        {
            attempts++;
            if (attempts < 2)
            {
                throw new InvalidOperationException("Fail once for null provider fallback");
            }
            return ValueTask.FromResult("NullProviderFallbackSuccess");
        });

        var result = await behavior.Handle(command, next, CancellationToken.None);

        result.Should().Be("NullProviderFallbackSuccess");
        attempts.Should().Be(2);
    }

    [Fact]
    public async Task Handle_WithCircuitBreakerPipeline_TripsCircuitAndThrowsBrokenCircuitExceptionOnSubsequentCalls()
    {
        var circuitBreakerPipeline = new ResiliencePipelineBuilder()
            .AddCircuitBreaker(new()
            {
                FailureRatio = 0.5,
                SamplingDuration = TimeSpan.FromSeconds(10),
                MinimumThroughput = 2,
                BreakDuration = TimeSpan.FromMinutes(1)
            })
            .Build();

        var behavior = new PollyResilienceBehavior<PollyPingCommand, string>(defaultPipeline: circuitBreakerPipeline);
        var command = new PollyPingCommand("CircuitTest");

        // Attempt 1: Fails with InvalidOperationException
        var nextFail1 = new DelegateNext<string>(() => throw new InvalidOperationException("Fail 1"));
        var act1 = async () => await behavior.Handle(command, nextFail1, CancellationToken.None).AsTask();
        await act1.Should().ThrowAsync<InvalidOperationException>();

        // Attempt 2: Fails with InvalidOperationException, tripping the breaker
        var nextFail2 = new DelegateNext<string>(() => throw new InvalidOperationException("Fail 2"));
        var act2 = async () => await behavior.Handle(command, nextFail2, CancellationToken.None).AsTask();
        await act2.Should().ThrowAsync<InvalidOperationException>();

        // Attempt 3: Circuit is now open -> throws BrokenCircuitException directly without executing next
        var executedNext = false;
        var nextShouldNotRun = new DelegateNext<string>(() =>
        {
            executedNext = true;
            return ValueTask.FromResult("Should not reach");
        });

        var act3 = async () => await behavior.Handle(command, nextShouldNotRun, CancellationToken.None).AsTask();
        await act3.Should().ThrowAsync<global::Polly.CircuitBreaker.BrokenCircuitException>();
        executedNext.Should().BeFalse("Circuit breaker is open so handler continuation must not be invoked");
    }

    [Fact]
    public async Task Handle_WithTimeoutPipeline_WhenOperationExceedsLimit_ThrowsTimeoutRejectedException()
    {
        var timeoutPipeline = new ResiliencePipelineBuilder()
            .AddTimeout(TimeSpan.FromMilliseconds(50))
            .Build();

        var behavior = new PollyResilienceBehavior<PollyPingCommand, string>(defaultPipeline: timeoutPipeline);
        var command = new PollyPingCommand("TimeoutTest");

        var nextSlow = new DelegateNext<string>(async () =>
        {
            await Task.Delay(TimeSpan.FromMilliseconds(500));
            return "SlowSuccess";
        });

        var act = async () => await behavior.Handle(command, nextSlow, CancellationToken.None).AsTask();
        await act.Should().ThrowAsync<global::Polly.Timeout.TimeoutRejectedException>();
    }

    [Fact]
    public async Task Handle_WithCompositePipeline_AppliesRetryAndTimeoutCohesively()
    {
        int attempt = 0;
        var compositePipeline = new ResiliencePipelineBuilder()
            .AddRetry(new()
            {
                MaxRetryAttempts = 2,
                Delay = TimeSpan.Zero,
                ShouldHandle = new PredicateBuilder().Handle<global::Polly.Timeout.TimeoutRejectedException>()
            })
            .AddTimeout(TimeSpan.FromMilliseconds(50))
            .Build();

        var behavior = new PollyResilienceBehavior<PollyPingCommand, string>(defaultPipeline: compositePipeline);
        var command = new PollyPingCommand("CompositeTest");

        var next = new DelegateNext<string>(async () =>
        {
            attempt++;
            if (attempt == 1)
            {
                // First attempt times out
                await Task.Delay(TimeSpan.FromMilliseconds(300));
            }
            // Second attempt succeeds immediately
            return await ValueTask.FromResult("CompositeSuccess");
        });

        var result = await behavior.Handle(command, next, CancellationToken.None);

        result.Should().Be("CompositeSuccess");
        attempt.Should().Be(2);
    }

    private static ResiliencePipeline BuildRetryPipeline(int maxRetryAttempts = 2, TimeSpan? delay = null)
    {
        return new ResiliencePipelineBuilder()
            .AddRetry(new()
            {
                MaxRetryAttempts = maxRetryAttempts,
                Delay = delay ?? TimeSpan.Zero
            })
            .Build();
    }
}
