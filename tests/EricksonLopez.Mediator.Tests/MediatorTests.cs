// Copyright © Erickson Lopez. MIT License.
using System;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using EricksonLopez.Mediator;
using EricksonLopez.Mediator.Generated;
using EricksonLopez.Mediator.Tests.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EricksonLopez.Mediator.Tests;

public class MediatorTests
{
    private IMediator CreateMediator(Action<IServiceCollection>? configure = null, TestStateTracker? tracker = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton(tracker ?? new TestStateTracker());
        services.AddEricksonLopezMediator();
        configure?.Invoke(services);
        return services.BuildServiceProvider().GetRequiredService<IMediator>();
    }

    [Fact]
    public async Task Send_PingCommand_ReturnsPong()
    {
        var mediator = CreateMediator();
        var result = await mediator.Send(new PingCommand());
        result.Should().Be("Pong");
    }

    [Fact]
    public async Task Send_MyQuery_ReturnsQueryResult()
    {
        var mediator = CreateMediator();
        var result = await mediator.Send(new MyQuery());
        result.Should().Be(42);
    }

    [Fact]
    public async Task Publish_MyNotification_InvokesAllRegisteredHandlers()
    {
        var tracker = new TestStateTracker();
        var mediator = CreateMediator(tracker: tracker);

        await mediator.Publish(new MyNotification());

        tracker.WasInvoked(nameof(NotificationHandler1)).Should().BeTrue();
        tracker.WasInvoked(nameof(NotificationHandler2)).Should().BeTrue();
    }

    [Fact]
    public async Task Send_OrderedCommand_ExecutesPipelineBehaviorsInConfiguredOrder()
    {
        var log = new OrderLog();
        var mediator = CreateMediator(s => s.AddSingleton(log));

        var result = await mediator.Send(new OrderedCommand());

        result.Should().Be(100);
        log.Log.Should().Equal(1, 2, -2, -1);
    }

    [Fact]
    public async Task Publish_ParallelNotification_InvokesAllHandlersConcurrently()
    {
        var tracker = new TestStateTracker();
        var mediator = CreateMediator(tracker: tracker);

        await mediator.Publish(new ParallelNotification());

        tracker.WasInvoked(nameof(ParallelNotificationHandler1)).Should().BeTrue();
        tracker.WasInvoked(nameof(ParallelNotificationHandler2)).Should().BeTrue();
    }

    [Fact]
    public async Task Send_SingleBehaviorCommand_ExecutesBehaviorAroundHandler()
    {
        var log = new OrderLog();
        var mediator = CreateMediator(s => s.AddSingleton(log));

        var result = await mediator.Send(new SingleBehaviorCommand());

        result.Should().Be(50);
        log.Log.Should().Equal(1, -1);
    }

    [Fact]
    public async Task Send_CancelCommand_PropagatesCancellationTokenToHandler()
    {
        var tracker = new TestStateTracker();
        var mediator = CreateMediator(tracker: tracker);
        using var cts = new CancellationTokenSource();

        await mediator.Send(new CancelCommand(), cts.Token);

        tracker.GetToken(nameof(CancelCommandHandler)).Should().Be(cts.Token);
    }

    [Fact]
    public async Task Send_CancelCommandWithBehavior_PropagatesCancellationTokenToBehavior()
    {
        var tracker = new TestStateTracker();
        var mediator = CreateMediator(tracker: tracker);
        using var cts = new CancellationTokenSource();

        await mediator.Send(new CancelCommandWithBehavior(), cts.Token);

        tracker.GetToken(nameof(CancelBehaviorForCancelCommandWithBehavior)).Should().Be(cts.Token);
    }

    [Fact]
    public async Task Send_PreCancelledCancellationToken_DoesNotInvokeHandler()
    {
        var tracker = new TestStateTracker();
        var mediator = CreateMediator(tracker: tracker);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = async () => await mediator.Send(new PreCancelledCommand(), cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        tracker.WasInvoked(nameof(PreCancelledCommandHandler)).Should().BeFalse();
    }

    [Fact]
    public async Task Send_PreCancelledCancellationToken_DoesNotInvokeQueryHandler()
    {
        var tracker = new TestStateTracker();
        var mediator = CreateMediator(tracker: tracker);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = async () => await mediator.Send(new PreCancelledQuery(), cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        tracker.WasInvoked(nameof(PreCancelledQueryHandler)).Should().BeFalse();
    }

    [Fact]
    public async Task Publish_PreCancelledCancellationToken_DoesNotInvokeNotificationHandler()
    {
        var tracker = new TestStateTracker();
        var mediator = CreateMediator(tracker: tracker);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = async () => await mediator.Publish(new PreCancelledNotification(), cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        tracker.WasInvoked(nameof(PreCancelledNotificationHandler)).Should().BeFalse();
    }

    [Fact]
    public async Task Send_ThrowCommand_PropagatesHandlerExceptionToCaller()
    {
        var mediator = CreateMediator();

        var act = async () => await mediator.Send(new ThrowCommand()).AsTask();
        var ex = await act.Should().ThrowAsync<InvalidOperationException>();

        ex.WithMessage("handler-exception");
    }

    [Fact]
    public async Task Send_OuterCommand_DispatchesNestedInnerCommandSuccessfully()
    {
        var services = new ServiceCollection();
        services.AddSingleton(new TestStateTracker());
        services.AddEricksonLopezMediator();
        var sp = services.BuildServiceProvider();
        var mediator = sp.GetRequiredService<IMediator>();

        var result = await mediator.Send(new OuterCommand());

        result.Should().Be("outer:Pong");
    }

    [Fact]
    public async Task Send_SingletonCommand_ReturnsSameSingletonHandlerInstanceAcrossCalls()
    {
        var services = new ServiceCollection();
        services.AddSingleton(new TestStateTracker());
        services.AddEricksonLopezMediator();
        var sp = services.BuildServiceProvider();
        var mediator = sp.GetRequiredService<IMediator>();

        var id1 = await mediator.Send(new SingletonCommand());
        var id2 = await mediator.Send(new SingletonCommand());

        id1.Should().Be(id2);
    }

    [Fact]
    public async Task Send_ScopedCommand_ResolvesUniqueInstancePerScope()
    {
        var services = new ServiceCollection();
        services.AddSingleton(new TestStateTracker());
        services.AddEricksonLopezMediator();
        var sp = services.BuildServiceProvider();

        // Within the same scope, the handler should be the same instance
        using var scope = sp.CreateScope();
        var h1 = scope.ServiceProvider.GetRequiredService<ScopedCommandHandler>();
        var h2 = scope.ServiceProvider.GetRequiredService<ScopedCommandHandler>();
        h1.Should().BeSameAs(h2);

        // The mediator (singleton) can dispatch to a scoped handler
        var mediator = sp.GetRequiredService<IMediator>();
        var id = await mediator.Send(new ScopedCommand());
        id.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task Send_CommandWithBehavior_AppliesBehaviorOnlyToTargetRequest()
    {
        var tracker = new BehaviorTracker();
        var mediator = CreateMediator(s => s.AddSingleton(tracker));

        // CommandWithBehavior has [UseBehavior(typeof(TrackedBehavior))]
        await mediator.Send(new CommandWithBehavior());
        tracker.WasInvoked.Should().BeTrue("TrackedBehavior should have been invoked for CommandWithBehavior");

        // CommandWithoutBehavior has no behaviors — tracker must NOT be invoked
        tracker.Reset();
        await mediator.Send(new CommandWithoutBehavior());
        tracker.WasInvoked.Should().BeFalse("TrackedBehavior must NOT be invoked for CommandWithoutBehavior");
    }

    [Fact]
    public async Task Send_FiveBehaviorCommand_ExecutesAllFiveBehaviorsInCorrectOrder()
    {
        var log = new OrderLog();
        var mediator = CreateMediator(s => s.AddSingleton(log));

        await mediator.Send(new FiveBehaviorCommand());

        // B1(1)→B2(2)→B3(3)→B4(4)→B5(5)→handler→B5(-5)→B4(-4)→B3(-3)→B2(-2)→B1(-1)
        log.Log.Should().Equal(1, 2, 3, 4, 5, -5, -4, -3, -2, -1);
    }

    [Fact]
    public async Task Publish_AggregatedNotification_AggregatesAllHandlerExceptions()
    {
        var mediator = CreateMediator();

        var act = async () => await mediator.Publish(new AggregatedNotification()).AsTask();
        var ex = await act.Should().ThrowAsync<NotificationHandlerAggregateException>();

        ex.Which.HandlerExceptions.Should().HaveCount(2);
        ex.Which.HandlerExceptions.Should().Contain(e => e is InvalidOperationException && e.Message == "First aggregated error");
        ex.Which.HandlerExceptions.Should().Contain(e => e is ArgumentException && e.Message == "Second aggregated error");
    }
}
